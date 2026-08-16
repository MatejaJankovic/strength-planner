# Trening nastaje samo kroz dugoročni plan

**Grana:** `feature/macrocycle-first`

## Problem: dva ekrana za istu stvar, i dugme koje je vraćalo obrisano

Aplikacija je imala **dva puta** do treninga: „Napravi mezociklus" (`/mesocycle`) i
„Napravi plan" (`/plan`). Iz korisnikovog ugla su izgledali kao dve različite stvari.

U bazi to nikada nisu bile dve stvari. `POST /api/mesocycles` je radio ovo:

```csharp
var plan = await _macrocycleService.CreateAsync(userId, new CreateMacrocycleRequest
{
    Name = request.Name,
    StartDate = request.StartDate,
    Blocks = [ new CreateMacrocycleBlockDto { ... } ]
}, cancellationToken);
```

Dakle: **pravio je plan sa jednim blokom** i vraćao njegov mezociklus. Svaki mezociklus je
oduvek pripadao nekom planu; razlika je bila samo u tome što je jedan ekran to skrivao.

Posledice su bile stvarne:

- Ekran „Trening" je nudio **„Novi plan"**, koji je usred dugoročnog plana pravio nov plan
  sa jednim blokom i gasio zatečeni - bez ijedne reči upozorenja.
- Ekran „Trening" je nudio **„Obriši plan"**, koji je brisao mezociklus bloka. Blok bi
  ostao bez svog treninga, a `MacrocycleService.EnsureCurrentBlockAsync` - koji se poziva
  **pri svakom čitanju plana** - to stanje prepoznaje kao „prvi blok još nije generisan" i
  ponovo ga generiše:

  ```csharp
  var live = blocks.Where(block => block.MesocycleId.HasValue).ToList();
  if (live.Count == 0)
  {
      await RegenerateBlockAsync(userId, plan.Id, blocks[0].Id, cancellationToken);
      return;
  }
  ```

  Otud prijava: obrišeš mezociklus, odeš na „Plan", vratiš se - i on je opet tu, aktivan.
  Brisanje i samopopravka su radili jedno protiv drugog, a samopopravka je uvek pobeđivala.

## Rešenje

### Jedan ulaz

`/mezociklus` ekran i ruta su uklonjeni. Sve što je nudio - šablon, cilj, raspored, naziv i
datum - čarobnjak dugoročnog plana je već imao. **Plan sa jednim blokom je tačno ono što je
ranije bio „samo mezociklus"**, pa se nijedna mogućnost ne gubi.

Uklonjeni su i `POST /api/mesocycles` i `DELETE /api/mesocycles/{id}`, zajedno sa
`IMesocycleService.DeleteAsync`. Pravilo je time **provereno na serveru, a ne samo sakriveno
u interfejsu** - i, što je važnije, nestao je jedini put kojim je blok mogao da ostane bez
svog mezociklusa. `MesocyclesController` sada samo čita.

Onboarding posle unosa 1RM vrednosti vodi na `/plan`. Prazno stanje ekrana „Trening" vodi
na isto mesto.

### Brisanje na nivou plana

Plan se briše sa ekrana „Plan", uz potvrdu koja kaže šta se gubi. `DeleteAsync` briše
mezocikluse blokova **izričito**:

```csharp
var mesocycles = await _db.Mesocycles
    .Where(mesocycle => mesocycle.UserId == userId && mesocycleIds.Contains(mesocycle.Id))
    .ToListAsync(cancellationToken);

_db.Mesocycles.RemoveRange(mesocycles);
_db.Macrocycles.Remove(macrocycle);
```

Kaskada plan → blokovi mezocikluse **ne** dodiruje, jer je strani ključ bloka na mezociklus
`SetNull` - to postoji da brisanje mezociklusa ne bi obaralo ceo plan. Ovde je smer suprotan,
pa se moraju ukloniti ručno; inače bi ostali u bazi bez ijednog ekrana sa kog se vide.

Pet novih testova (`PlanDeletionTests`) drži baš taj razlog: da je ključ ikada prebačen na
`Cascade`, izričito brisanje bi postalo suvišno; na `Restrict`, brisanje plana bi pucalo.
Testovi rade nad EF modelom bez otvaranja veze ka bazi, isto kao `OwnershipFilterTests`.

### Sadržaj šablona se opet vidi

Ekran za mezociklus je bio **jedino** mesto koje je pokazivalo šta šablon nosi - dane, vežbe
i upozorenja poput onog za dvodnevni plan. Čarobnjak plana je imao samo padajući meni sa
nazivima, pa bi se posle uklanjanja dugoročan plan sastavljao od šablona čije vežbe korisnik
nigde ne vidi.

Zato blok u čarobnjaku sada ispod menija ispisuje dane i vežbe izabranog šablona, a sam meni
razdvaja **„Moji šabloni"** od **„Ugrađeni šabloni"** kroz `optgroup`, kad ličnih ima.

## Provera

- `dotnet build`, `dotnet test` (**287**, sa 5 novih), `npm run build`, `npm test`
  (**29**, sa 7 novih) - sve prolazi.
- Četiri nova testa komponente pokrivaju brisanje: da zahtev ide na plan a ne na mezociklus,
  da posle njega ne ostane ni plan ni keširan trening, da potvrda zaista traži potvrdu, i da
  neuspelo brisanje ostavlja plan na ekranu uz poruku.
### Prolaz kroz aplikaciju sa prijavljenim nalogom

**Pravilo je provereno na serveru, ne samo na ekranu** - pozivi su poslati direktno:

| Poziv | Odgovor |
|---|---|
| `POST /api/mesocycles` | **405** (metoda više ne postoji) |
| `DELETE /api/mesocycles/{id}` | **405** |
| `GET /api/mesocycles` | 200 (čitanje i dalje radi) |
| `DELETE /api/macrocycles/{tuđ-id}` | 404 (vlasništvo se poštuje) |

**Prijavljena greška više ne postoji.** Napravljen je plan sa dva bloka, obrisan sa ekrana
„Plan", pa je ponovljen tačno onaj niz koji ju je izazivao - odlazak na „Trening" i povratak
na „Plan":

- ekran pokazuje prazno stanje, bez ijednog bloka;
- `GET /macrocycles/active` vraća **404 pri svakom od tri uzastopna čitanja**. To je poziv
  koji pokreće `EnsureCurrentBlockAsync`, dakle upravo mesto na kom se ranije obrisano
  vraćalo;
- `GET /mesocycles/active` takođe 404 - mezociklus je otišao sa planom, a ne ostao u bazi.

**Ekran „Trening" više ništa ne pravi ni ne briše**: na njemu nema ni „Novi plan" ni
„Obriši plan".

**Sadržaj šablona se vidi u čarobnjaku**: ispod menija svakog bloka stoje dani i vežbe
(`Day A: Back Squat · Bench Press · …`, `Day B: Overhead Press · …`). Podela na „Moji
šabloni" i „Ugrađeni šabloni" se pojavljuje tek kad korisnik ima lični šablon; na nalogu bez
njih meni ostaje ravan, kako i treba.

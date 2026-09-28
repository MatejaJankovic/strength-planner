# Dva uslova iz rada koja aplikacija rešava drugačije — i ćutala o tome

Nalaz D23c iz pregleda logike treninga.

## Šta rad kaže

Slučaj korišćenja 4 („Креирање мезоциклуса") ima dva izuzetka:

> *Изузеци: већ постоји активан мезоциклус - систем **захтева да се текући заврши или
> обрише**; за неку вежбу не постоји процена 1RM - систем **тражи допуну уноса**.*

Kod je odlučio drugačije o oba, i o oba je ćutao.

## 1. Aktivan plan — odluka ostaje, ćutanje ne

Sedma runda je namerno izabrala drugačije ponašanje: nov plan **gasi** prethodni umesto da
odbije da bude napravljen. To je zapisano i u uputstvu, i ostaje.

Problem je bio što aplikacija to nigde nije rekla. Dugme „Napravi plan" je bez ijedne reči
završavalo plan koji je vežbač pratio — a plan je mogao da bude na desetoj od dvanaest
nedelja.

Sada čarobnjak **stane i pita**, imenujući plan koji se gasi:

> ⚠ Plan **„Zima 2026"** prestaje da bude aktivan. Ne briše se — odrađeni treninzi i procene
> maksimuma ostaju — ali se na njega više ne vraćaš kroz ekran „Trening". Nastaviti?

Potvrda je drugi klik. Odustajanje ne šalje ništa i **ne briše uneto** — naziv, datum i
blokovi ostaju kakvi su bili.

Dugme za odustajanje piše „Ne, zadrži trenutni", a ne „Odustani": pored njega već stoji
dugme koje zatvara ceo čarobnjak, i dva „Odustani" jedno uz drugo ne bi značila isto.

## 2. Vežba bez maksimuma — sada se traži, ali ne zahteva

Vežba bez poznatog 1RM-a je tiho čekala da joj vežbač prvi put unese težinu „po osećaju".
To jeste legitiman put — i uputstvo ga nudi — ali je bio **jedini**, i neizrečen.

Ekran „Trening" sada na vrhu piše koliko vežbi u bloku nema poznat maksimum, nabroji prve
tri i ponudi dugme ka ekranu za unos. Nije prepreka: plan radi i bez njih.

### Odakle se to zna

Iz **spiska maksimuma**, a ne iz upisanog cilja u planu.

Prva verzija je gledala `targetWeightKg == null` i izlistala **osamnaest** vežbi — među
njima i dve za koje maksimum postoji. Razlog: cilj u planu je prazan i kada maksimum
postoji ali ga blok nije pokupio, na primer kada je unet posle generisanja. Pitanje je
„postoji li odgovor za ovu vežbu", a odgovor živi u zapisima maksimuma.

Vežbe koje diže sopstvena masa se ne broje: njima je nula dodatih tačan prvi propis, a ne
nepoznanica.

Spisak se skraćuje na tri imena i broj. Nalog bez ijednog unetog maksimuma bi inače nabrojao
ceo blok, a zid od osamnaest imena ne govori više od broja.

## Šta je izmereno

Uživo, nalog sa aktivnim planom „D20 UI" i unetim maksimumima za čučanj i potisak:

| Korak | Rezultat |
|---|---|
| „Napravi plan" sa popunjenim nazivom | pojavi se upozorenje sa imenom **„D20 UI"**, ništa se ne šalje |
| „Ne, zadrži trenutni" | upozorenje nestaje, naziv „D23c provera" ostaje u polju, stari plan i dalje aktivan |
| „Napravi plan" pa „Da, napravi novi plan" | nov plan napravljen, stari ugašen |
| ekran „Trening" | *„16 vežbi u ovom bloku nema poznat maksimum… Barbell Curl, Cable Crunch, Cable Fly i još 13"* |

Šesnaest, a ne osamnaest: čučanj i potisak su izuzeti jer za njih maksimum postoji. To je
tačno razlika koju je prva verzija promašila.

## Šta ovo ne rešava

Rad i dalje kaže „захтева да се текући заврши или обрише". Aplikacija to **ne radi** i neće:
odluka iz sedme runde je da plan bude jedini ulaz u trening i da se novi pravi bez brisanja
starog. Ovo je rečenica u radu koju treba uskladiti sa aplikacijom, a ne obrnuto — jer
zahtevati brisanje istorije da bi se počeo nov blok nije dobra odluka, i nije ona koja je
doneta.

## Testovi

`plan-home.spec.ts` dobija dve tvrdnje: da prvi klik samo postavlja pitanje i ne šalje
nijedan `POST`, i da odustajanje ne šalje zahtev niti briše uneto.

Ukupno: 611 testova na serveru (bez izmena), 142 → 144 na klijentu.

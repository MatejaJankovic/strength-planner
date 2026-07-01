import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { PersonalRecordDto } from '../../core/models/analytics.models';

@Component({
  selector: 'app-pr-list',
  imports: [DecimalPipe, DatePipe, MatIconModule],
  templateUrl: './pr-list.html',
  styleUrl: './pr-list.scss',
})
export class PrList {
  readonly records = input<PersonalRecordDto[]>([]);
}

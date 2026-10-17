import { Component, input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { PayrollResultDto } from '../../api/models/payroll-result-dto';
import { formatHours, formatInstantDateTime } from '../../core/date-format';
import { formatMoney } from '../../core/format';

@Component({
  selector: 'app-payroll-card',
  imports: [MatCardModule],
  templateUrl: './payroll-card.html',
  styleUrl: './payroll-card.scss',
})
export class PayrollCard {
  readonly result = input.required<PayrollResultDto>();
  readonly name = input.required<string>();

  protected readonly formatMoney = formatMoney;
  protected readonly formatHours = formatHours;
  protected readonly formatInstantDateTime = formatInstantDateTime;
}

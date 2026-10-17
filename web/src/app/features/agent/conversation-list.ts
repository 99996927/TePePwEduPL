import { Component, computed, inject, input, model, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { ConversationSummaryDto } from '../../api/models/conversation-summary-dto';
import { formatInstantDateTime } from '../../core/date-format';
import { EmployeeDirectory } from '../../core/employee-directory';
import { SessionService } from '../../core/session';

@Component({
  selector: 'app-conversation-list',
  imports: [MatButtonModule, MatProgressBarModule, MatSlideToggleModule],
  templateUrl: './conversation-list.html',
  styleUrl: './conversation-list.scss',
})
export class ConversationList {
  private readonly directory = inject(EmployeeDirectory);
  private readonly session = inject(SessionService);

  readonly conversations = input<ConversationSummaryDto[]>([]);
  readonly selectedId = input<string | null>(null);
  readonly showAuthor = input(false);
  readonly loading = input(false);
  readonly error = input<string | null>(null);
  readonly onlyMine = model(false);

  readonly select = output<string>();
  readonly create = output<void>();
  readonly retry = output<void>();

  protected readonly visible = computed(() => {
    const all = this.conversations();
    if (!this.showAuthor() || !this.onlyMine()) return all;
    const me = this.session.user()?.userId;
    return all.filter((c) => c.userId === me);
  });

  protected readonly formatDateTime = formatInstantDateTime;

  protected author(userId: string): string {
    return this.directory.userLabel(userId);
  }
}

import { Component, computed, input } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MessageDto } from '../../api/models/message-dto';

@Component({
  selector: 'app-conversation-thread',
  imports: [MatProgressBarModule],
  templateUrl: './conversation-thread.html',
  styleUrl: './conversation-thread.scss',
})
export class ConversationThread {
  readonly messages = input<MessageDto[]>([]);
  readonly pending = input(false);
  readonly ungroundedNumbers = input<string[]>([]);
  readonly loading = input(false);
  readonly emptyHint = input('');

  protected readonly warning = computed(() => {
    const numbers = this.ungroundedNumbers();
    return numbers.length > 0 ? `Liczby nie pochodzą z danych systemu: ${numbers.join(', ')}.` : null;
  });

  protected readonly lastAssistantSeq = computed(() => {
    const list = this.messages();
    for (let i = list.length - 1; i >= 0; i--) if (list[i].role === 'Assistant') return list[i].seq;
    return null;
  });
}

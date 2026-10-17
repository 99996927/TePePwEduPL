import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { ActivatedRoute, Router } from '@angular/router';
import { distinctUntilChanged, firstValueFrom, map } from 'rxjs';
import { ConversationSummaryDto } from '../../api/models/conversation-summary-dto';
import { MessageDto } from '../../api/models/message-dto';
import { ConversationsService } from '../../api/services/conversations.service';
import { messageOf, problemOf } from '../../core/api-problem';
import { EmployeeDirectory } from '../../core/employee-directory';
import { Notifier } from '../../core/notifier';
import { HR_ROLES } from '../../core/roles';
import { SessionService } from '../../core/session';
import { ConversationList } from './conversation-list';
import { ConversationThread } from './conversation-thread';

interface OpenConversation {
  id: string;
  userId: string;
  messages: MessageDto[];
}

const EMPTY_HINT = 'Zadaj pytanie agentowi, np. „Ile mam dni urlopu?”.';

@Component({
  selector: 'app-agent',
  imports: [MatButtonModule, ConversationList, ConversationThread],
  templateUrl: './agent.html',
  styleUrl: './agent.scss',
})
export class Agent {
  private readonly api = inject(ConversationsService);
  private readonly router = inject(Router);
  private readonly session = inject(SessionService);
  private readonly notifier = inject(Notifier);
  private readonly directory = inject(EmployeeDirectory);

  protected readonly showAuthor = this.session.hasRole(...HR_ROLES);

  protected readonly conversations = signal<ConversationSummaryDto[]>([]);
  protected readonly loadingList = signal(true);
  protected readonly listError = signal<string | null>(null);
  protected readonly current = signal<OpenConversation | null>(null);
  protected readonly loadingConversation = signal(false);
  protected readonly conversationError = signal<string | null>(null);
  protected readonly onlyMine = signal(false);

  /** Conversation id from the route (`/agent/<id>`); null for a new conversation. */
  protected readonly selectedId = toSignal(
    inject(ActivatedRoute).paramMap.pipe(map((p) => p.get('id'))),
    { initialValue: null },
  );

  /** `new`: no id in the route; `own`/`readonly`: the open conversation is loaded; `none`: loading or failed. */
  protected readonly mode = computed<'new' | 'own' | 'readonly' | 'none'>(() => {
    const id = this.selectedId();
    if (id === null) return 'new';
    const open = this.current();
    if (open === null || open.id !== id) return 'none';
    return open.userId === this.session.user()?.userId ? 'own' : 'readonly';
  });

  protected readonly authorName = computed(() => this.directory.nameOfUser(this.current()?.userId));
  protected readonly messages = computed(() => this.current()?.messages ?? []);
  protected readonly emptyHint = computed(() => (this.mode() === 'new' ? EMPTY_HINT : ''));

  private listSeq = 0;
  private conversationSeq = 0;

  constructor() {
    this.directory.load().catch((e: unknown) => this.notifier.error(problemOf(e)));
    void this.loadList();
    inject(ActivatedRoute)
      .paramMap.pipe(
        map((p) => p.get('id')),
        distinctUntilChanged(),
        takeUntilDestroyed(),
      )
      .subscribe((id) => this.open(id));
  }

  protected select(id: string): void {
    void this.router.navigate(['/agent', id]);
  }

  protected create(): void {
    void this.router.navigate(['/agent']);
  }

  protected retryList(): void {
    void this.loadList();
  }

  protected retryConversation(): void {
    const id = this.selectedId();
    if (id !== null) void this.loadConversation(id);
  }

  /** Shows a conversation already known locally (e.g. after the first answer) without fetching it; a later route change to this id does not fetch either. */
  protected openLocal(id: string, messages: MessageDto[], userId: string): void {
    this.conversationSeq++;
    this.current.set({ id, userId, messages });
    this.loadingConversation.set(false);
    this.conversationError.set(null);
  }

  private open(id: string | null): void {
    if (id === null) {
      this.conversationSeq++;
      this.current.set(null);
      this.loadingConversation.set(false);
      this.conversationError.set(null);
      return;
    }
    if (this.current()?.id === id) {
      // Already loaded locally; drop any in-flight request for another id.
      this.conversationSeq++;
      this.loadingConversation.set(false);
      this.conversationError.set(null);
      return;
    }
    void this.loadConversation(id);
  }

  private async loadList(): Promise<void> {
    const seq = ++this.listSeq;
    this.loadingList.set(true);
    try {
      const list = await firstValueFrom(this.api.conversationsList());
      if (seq !== this.listSeq) return;
      this.conversations.set(list);
      this.listError.set(null);
    } catch (e) {
      if (seq !== this.listSeq) return;
      this.listError.set(messageOf(problemOf(e)));
    } finally {
      if (seq === this.listSeq) this.loadingList.set(false);
    }
  }

  private async loadConversation(id: string): Promise<void> {
    const seq = ++this.conversationSeq;
    this.current.set(null);
    this.conversationError.set(null);
    this.loadingConversation.set(true);
    try {
      const detail = await firstValueFrom(this.api.conversationsGet({ id }));
      if (seq !== this.conversationSeq) return;
      this.current.set({ id: detail.id, userId: detail.userId, messages: detail.messages });
    } catch (e) {
      if (seq !== this.conversationSeq) return;
      const problem = problemOf(e);
      if (problem.status === 404) {
        this.notifier.error('Nie znaleziono rozmowy.');
        void this.router.navigate(['/agent']);
      } else {
        this.conversationError.set(messageOf(problem));
      }
    } finally {
      if (seq === this.conversationSeq) this.loadingConversation.set(false);
    }
  }
}

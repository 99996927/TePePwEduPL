import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router } from '@angular/router';
import { distinctUntilChanged, firstValueFrom, map } from 'rxjs';
import { ConversationSummaryDto } from '../../api/models/conversation-summary-dto';
import { MessageDto } from '../../api/models/message-dto';
import { ConversationsService } from '../../api/services/conversations.service';
import { ApiProblem, messageOf, problemOf } from '../../core/api-problem';
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

export const MAX_QUESTION_LENGTH = 4000;

const EMPTY_HINT = 'Zadaj pytanie agentowi, np. „Ile mam dni urlopu?”.';

@Component({
  selector: 'app-agent',
  imports: [MatButtonModule, MatFormFieldModule, MatInputModule, ConversationList, ConversationThread],
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

  protected readonly maxLength = MAX_QUESTION_LENGTH;
  protected readonly text = signal('');
  protected readonly sending = signal(false);
  protected readonly sendError = signal<string | null>(null);
  protected readonly ungroundedNumbers = signal<string[]>([]);
  /** The question shown immediately after sending, until the server returns the saved messages. */
  private readonly localQuestion = signal<MessageDto | null>(null);

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
  protected readonly messages = computed(() => {
    const saved = this.current()?.messages ?? [];
    const local = this.localQuestion();
    return local === null ? saved : [...saved, local];
  });
  /** The composer is shown for a new or own conversation, but not while one loads or after it failed to load. */
  protected readonly showComposer = computed(
    () =>
      (this.mode() === 'new' || this.mode() === 'own') && !this.loadingConversation() && this.conversationError() === null,
  );
  protected readonly tooLong = computed(() => this.text().length > MAX_QUESTION_LENGTH);
  protected readonly canSend = computed(() => !this.sending() && this.text().trim().length > 0 && !this.tooLong());
  protected readonly emptyHint = computed(() => (this.mode() === 'new' ? EMPTY_HINT : ''));

  private listSeq = 0;
  /** Shared by conversation loads and sending: any change of the open conversation makes older responses stale. */
  private conversationSeq = 0;
  /** The id of the last requested conversation, so a failed reload after a send error can be retried before the route has it. */
  private lastRequestedId: string | null = null;

  constructor() {
    this.directory.load().catch((e: unknown) => this.notifier.error(problemOf(e)));
    void this.loadList();
    // Responses arriving after the screen is gone must not navigate or touch state.
    inject(DestroyRef).onDestroy(() => {
      this.listSeq++;
      this.conversationSeq++;
    });
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
    const id = this.selectedId() ?? this.lastRequestedId;
    if (id !== null) void this.reloadFromServer(id);
  }

  protected onInput(event: Event): void {
    this.text.set((event.target as HTMLTextAreaElement).value);
  }

  /** Enter sends, Shift+Enter inserts a new line. */
  protected onKeydown(event: KeyboardEvent): void {
    if (event.key !== 'Enter' || event.shiftKey || event.isComposing) return;
    event.preventDefault();
    void this.send();
  }

  protected async send(): Promise<void> {
    if (!this.canSend()) return;
    const text = this.text().trim();
    const open = this.current();
    const conversationId = open?.id ?? null;
    const saved = open?.messages ?? [];
    const seq = ++this.conversationSeq;
    this.sending.set(true);
    this.sendError.set(null);
    this.ungroundedNumbers.set([]);
    this.localQuestion.set({
      seq: (saved.length > 0 ? saved[saved.length - 1].seq : 0) + 1,
      role: 'User',
      content: text,
      createdAtUtc: new Date().toISOString(),
      toolName: null,
      toolParams: null,
    });
    try {
      const response = await firstValueFrom(this.api.conversationsAsk({ body: { conversationId, text } }));
      if (seq !== this.conversationSeq) return;
      const userId = open?.userId ?? this.session.user()?.userId ?? '';
      this.endSending();
      this.text.set('');
      this.ungroundedNumbers.set(response.ungroundedNumbers);
      this.openLocal(response.conversationId, [...saved, ...response.messages], userId);
      if (conversationId === null) void this.router.navigate(['/agent', response.conversationId], { replaceUrl: true });
      // Refreshed after every answer so the list shows the current message count.
      void this.loadList();
    } catch (e) {
      if (seq !== this.conversationSeq) return;
      this.endSending();
      this.handleSendError(problemOf(e), conversationId);
    }
  }

  private handleSendError(problem: ApiProblem, conversationId: string | null): void {
    if (problem.status === 404) {
      this.notifier.error('Nie znaleziono rozmowy.');
      void this.router.navigate(['/agent']);
      return;
    }
    this.sendError.set(messageOf(problem));
    if (problem.status === 409 || problem.status === 503) {
      // The backend may have saved the question: show the conversation as stored.
      const id = problem.conversationId ?? conversationId;
      if (id !== null) void this.reloadFromServer(id);
    }
  }

  private endSending(): void {
    this.sending.set(false);
    this.localQuestion.set(null);
  }

  /** Fetches the conversation and, if the route does not show it yet (a new conversation), moves the address to it. */
  private async reloadFromServer(id: string): Promise<void> {
    if (!(await this.loadConversation(id))) return;
    if (this.selectedId() !== id) {
      void this.router.navigate(['/agent', id], { replaceUrl: true });
      void this.loadList();
    }
  }

  /** Shows a conversation already known locally (e.g. after the first answer) without fetching it; a later route change to this id does not fetch either. */
  protected openLocal(id: string, messages: MessageDto[], userId: string): void {
    this.conversationSeq++;
    this.current.set({ id, userId, messages });
    this.loadingConversation.set(false);
    this.conversationError.set(null);
  }

  private open(id: string | null): void {
    // Any in-flight send belongs to the previous conversation; its response is ignored via the sequence number.
    if (this.sending()) {
      // The backend already saved the question there; keeping it in the field would let Enter re-post it here.
      this.text.set('');
      this.notifier.info('Pytanie zostało wysłane w poprzedniej rozmowie.');
    }
    this.endSending();
    if (this.current()?.id !== id) {
      this.sendError.set(null);
      this.ungroundedNumbers.set([]);
    }
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

  /** Returns true when the conversation was loaded and is still the current request. */
  private async loadConversation(id: string): Promise<boolean> {
    const seq = ++this.conversationSeq;
    this.lastRequestedId = id;
    this.current.set(null);
    this.conversationError.set(null);
    this.loadingConversation.set(true);
    try {
      const detail = await firstValueFrom(this.api.conversationsGet({ id }));
      if (seq !== this.conversationSeq) return false;
      this.current.set({ id: detail.id, userId: detail.userId, messages: detail.messages });
      return true;
    } catch (e) {
      if (seq !== this.conversationSeq) return false;
      const problem = problemOf(e);
      if (problem.status === 404) {
        this.notifier.error('Nie znaleziono rozmowy.');
        void this.router.navigate(['/agent']);
      } else {
        this.conversationError.set(messageOf(problem));
      }
      return false;
    } finally {
      if (seq === this.conversationSeq) this.loadingConversation.set(false);
    }
  }
}

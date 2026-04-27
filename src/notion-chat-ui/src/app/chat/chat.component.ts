import { Component, ElementRef, OnInit, signal, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChatService } from '../services/chat.service';
import { ChatMessage } from '../models/chat.model';

@Component({
  selector: 'app-chat',
  imports: [FormsModule],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.scss',
})
export class ChatComponent implements OnInit {
  @ViewChild('messagesContainer') private messagesContainer!: ElementRef<HTMLDivElement>;

  messages = signal<ChatMessage[]>([]);
  userInput = signal('');
  isLoading = signal(false);
  isReady = signal(false);
  isConnecting = signal(false);
  errorMessage = signal('');
  private conversationId = '';

  constructor(private chatService: ChatService) {}

  ngOnInit(): void {
    this.startNewChat();
  }

  startNewChat(): void {
    this.messages.set([]);
    this.errorMessage.set('');
    this.conversationId = '';
    this.isReady.set(false);

    this.isConnecting.set(true);
    this.chatService.createConversation().subscribe({
      next: (res) => {
        this.conversationId = res.conversationId;
        this.isReady.set(true);
        this.isConnecting.set(false);
      },
      error: () => {
        this.isConnecting.set(false);
        this.errorMessage.set('Failed to start a new conversation. Is the backend server running on http://localhost:5000?');
      },
    });
  }

  sendMessage(): void {
    const text = this.userInput().trim();
    if (!text || this.isLoading() || !this.isReady()) return;

    this.messages.update(msgs => [...msgs, { role: 'user', content: text }]);
    this.userInput.set('');
    this.errorMessage.set('');
    this.isLoading.set(true);
    this.scrollToBottom();

    this.chatService.sendMessage(this.conversationId, text).subscribe({
      next: (res) => {
        this.messages.update(msgs => [...msgs, { role: 'assistant', content: res.reply }]);
        this.isLoading.set(false);
        this.scrollToBottom();
      },
      error: (err) => {
        this.isLoading.set(false);
        if (err.status === 0) {
          this.errorMessage.set('Unable to reach the server. Please check your connection.');
        } else if (err.status >= 500) {
          this.errorMessage.set('Server error. Please try again later.');
        } else {
          this.errorMessage.set(err.error?.message || 'Something went wrong. Please try again.');
        }
        this.scrollToBottom();
      },
    });
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const el = this.messagesContainer?.nativeElement;
      if (el) {
        el.scrollTop = el.scrollHeight;
      }
    });
  }
}

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  CreateConversationResponse,
  SendMessageRequest,
  SendMessageResponse,
} from '../models/chat.model';

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly apiUrl = 'http://localhost:5000/api/chat';

  constructor(private http: HttpClient) {}

  createConversation(): Observable<CreateConversationResponse> {
    return this.http.post<CreateConversationResponse>(
      `${this.apiUrl}/conversations`,
      {}
    );
  }

  sendMessage(
    conversationId: string,
    message: string
  ): Observable<SendMessageResponse> {
    const body: SendMessageRequest = { message };
    return this.http.post<SendMessageResponse>(
      `${this.apiUrl}/conversations/${conversationId}/messages`,
      body
    );
  }
}

export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface CreateConversationResponse {
  conversationId: string;
}

export interface SendMessageRequest {
  message: string;
}

export interface SendMessageResponse {
  reply: string;
}

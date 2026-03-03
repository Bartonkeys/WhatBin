import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { 
  IonHeader, 
  IonToolbar, 
  IonContent, 
  IonSpinner,
  IonIcon
} from '@ionic/angular/standalone';
import { BinService, BinLookupResponse, UserAddress } from '../services/bin.service';
import { ChatService } from '../services/chat.service';
import { NotificationService } from '../services/notification.service';
import { addIcons } from 'ionicons';
import {
  trashOutline, locationOutline, refreshOutline, searchOutline,
  calendarOutline, alertCircleOutline, chatbubblesOutline, sendOutline,
  notificationsOutline, checkmarkCircleOutline
} from 'ionicons/icons';

interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

@Component({
  selector: 'app-home',
  templateUrl: 'home.page.html',
  styleUrls: ['home.page.scss'],
  imports: [
    CommonModule,
    FormsModule,
    IonHeader, 
    IonToolbar, 
    IonContent,
    IonSpinner,
    IonIcon
  ],
})
export class HomePage implements OnInit {
  // Bin lookup
  postcode: string = '';
  houseNumber: string = '';
  binData: BinLookupResponse | null = null;
  loading: boolean = false;
  error: string = '';
  hasSearched: boolean = false;

  // Chat
  chatInput: string = '';
  messages: ChatMessage[] = [];
  chatLoading: boolean = false;
  chatSessionId: string | undefined;

  // SMS notifications
  phoneNumber: string = '';
  subscribing: boolean = false;
  subscribed: boolean = false;
  subscribeError: string = '';
  subscribeMessage: string = '';

  @ViewChild('chatMessages') chatMessagesEl!: ElementRef;

  constructor(
    private binService: BinService,
    private chatService: ChatService,
    private notificationService: NotificationService
  ) {
    addIcons({
      trashOutline, locationOutline, refreshOutline, searchOutline,
      calendarOutline, alertCircleOutline, chatbubblesOutline, sendOutline,
      notificationsOutline, checkmarkCircleOutline
    });
  }

  ngOnInit() {
    const savedAddress = this.binService.getSavedAddress();
    if (savedAddress) {
      this.postcode = savedAddress.postcode;
      this.houseNumber = savedAddress.houseNumber || '';
      this.lookupBins();
    }
  }

  lookupBins() {
    if (!this.postcode.trim()) {
      this.error = 'Please enter a postcode';
      return;
    }

    this.loading = true;
    this.error = '';
    this.hasSearched = true;

    this.binService.lookupBins(this.postcode.trim(), this.houseNumber.trim() || undefined)
      .subscribe({
        next: (data) => {
          this.binData = data;
          this.loading = false;
          
          const address: UserAddress = {
            postcode: this.postcode.trim(),
            houseNumber: this.houseNumber.trim() || undefined
          };
          this.binService.saveAddress(address);
        },
        error: (err) => {
          this.loading = false;
          this.error = 'Failed to fetch bin collection data. Please check your postcode and try again.';
          console.error('Error:', err);
        }
      });
  }

  clearData() {
    this.postcode = '';
    this.houseNumber = '';
    this.binData = null;
    this.error = '';
    this.hasSearched = false;
    this.binService.clearSavedAddress();
  }

  getColorClass(color: string): string {
    return `bin-${color.toLowerCase()}`;
  }

  // Chat methods
  sendSuggestion(text: string) {
    this.chatInput = text;
    this.sendChat();
  }

  sendChat() {
    const message = this.chatInput.trim();
    if (!message) return;

    this.messages.push({ role: 'user', content: message });
    this.chatInput = '';
    this.chatLoading = true;
    this.scrollChat();

    this.chatService.sendMessage({
      message,
      postcode: this.postcode.trim() || undefined,
      houseNumber: this.houseNumber.trim() || undefined,
      sessionId: this.chatSessionId
    }).subscribe({
      next: (response) => {
        this.chatSessionId = response.sessionId;
        this.messages.push({ role: 'assistant', content: response.reply });
        this.chatLoading = false;
        this.scrollChat();
      },
      error: (err) => {
        this.messages.push({
          role: 'assistant',
          content: 'Sorry, I encountered an error. Please try again.'
        });
        this.chatLoading = false;
        this.scrollChat();
        console.error('Chat error:', err);
      }
    });
  }

  private scrollChat() {
    setTimeout(() => {
      if (this.chatMessagesEl?.nativeElement) {
        this.chatMessagesEl.nativeElement.scrollTop = this.chatMessagesEl.nativeElement.scrollHeight;
      }
    }, 50);
  }

  // SMS subscription methods
  subscribeSms() {
    if (!this.phoneNumber.trim()) {
      this.subscribeError = 'Please enter your phone number';
      return;
    }
    if (!this.postcode.trim()) {
      this.subscribeError = 'Please look up your postcode first';
      return;
    }

    this.subscribing = true;
    this.subscribeError = '';

    this.notificationService.subscribe({
      phoneNumber: this.phoneNumber.trim(),
      postcode: this.postcode.trim(),
      houseNumber: this.houseNumber.trim() || undefined
    }).subscribe({
      next: (response) => {
        this.subscribing = false;
        this.subscribed = true;
        this.subscribeMessage = response.message;
        if (!response.smsEnabled) {
          this.subscribeMessage += ' (SMS service is being configured - you will start receiving messages soon)';
        }
      },
      error: (err) => {
        this.subscribing = false;
        this.subscribeError = err.error?.detail || 'Failed to subscribe. Please try again.';
        console.error('Subscribe error:', err);
      }
    });
  }
}

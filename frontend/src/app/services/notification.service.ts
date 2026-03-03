import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface SubscribeRequest {
  phoneNumber: string;
  postcode: string;
  houseNumber?: string;
}

export interface SubscribeResponse {
  message: string;
  subscriptionId: number;
  smsEnabled: boolean;
}

export interface UnsubscribeRequest {
  phoneNumber: string;
}

export interface SubscriptionStatus {
  phoneNumber: string;
  subscriptions: {
    id: number;
    postcode: string;
    houseNumber?: string;
    isActive: boolean;
    createdAt: string;
    lastNotifiedAt?: string;
  }[];
  smsEnabled: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private apiUrl = 'https://whatbinapi.onrender.com/api/notifications';

  constructor(private http: HttpClient) {}

  subscribe(request: SubscribeRequest): Observable<SubscribeResponse> {
    return this.http.post<SubscribeResponse>(`${this.apiUrl}/subscribe`, request);
  }

  unsubscribe(request: UnsubscribeRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/unsubscribe`, request);
  }

  getStatus(phoneNumber: string): Observable<SubscriptionStatus> {
    return this.http.get<SubscriptionStatus>(`${this.apiUrl}/status/${encodeURIComponent(phoneNumber)}`);
  }
}

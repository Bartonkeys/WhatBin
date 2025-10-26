import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';

export interface BinCollection {
  bin_type: string;
  color: string;
  next_collection: string;
}

export interface BinLookupResponse {
  address: string;
  collections: BinCollection[];
  next_collection_color: string;
}

export interface UserAddress {
  postcode: string;
  houseNumber?: string;
}

@Injectable({
  providedIn: 'root'
})
export class BinService {
  private apiUrl = 'https://app-isvwccpu.fly.dev/api';
  private storageKey = 'belfast_bins_address';

  constructor(private http: HttpClient) {}

  lookupBins(postcode: string, houseNumber?: string): Observable<BinLookupResponse> {
    return this.http.post<BinLookupResponse>(`${this.apiUrl}/bin-lookup`, {
      postcode,
      house_number: houseNumber
    }).pipe(
      catchError(error => {
        console.error('Error fetching bin data:', error);
        throw error;
      })
    );
  }

  saveAddress(address: UserAddress): void {
    localStorage.setItem(this.storageKey, JSON.stringify(address));
  }

  getSavedAddress(): UserAddress | null {
    const saved = localStorage.getItem(this.storageKey);
    if (saved) {
      try {
        return JSON.parse(saved);
      } catch (e) {
        console.error('Error parsing saved address:', e);
        return null;
      }
    }
    return null;
  }

  clearSavedAddress(): void {
    localStorage.removeItem(this.storageKey);
  }
}

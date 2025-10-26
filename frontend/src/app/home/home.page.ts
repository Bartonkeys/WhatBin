import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { 
  IonHeader, 
  IonToolbar, 
  IonTitle, 
  IonContent, 
  IonCard, 
  IonCardHeader, 
  IonCardTitle, 
  IonCardContent,
  IonItem,
  IonLabel,
  IonInput,
  IonButton,
  IonSpinner,
  IonIcon,
  IonList
} from '@ionic/angular/standalone';
import { BinService, BinLookupResponse, UserAddress } from '../services/bin.service';
import { addIcons } from 'ionicons';
import { trashOutline, locationOutline, refreshOutline } from 'ionicons/icons';

@Component({
  selector: 'app-home',
  templateUrl: 'home.page.html',
  styleUrls: ['home.page.scss'],
  imports: [
    CommonModule,
    FormsModule,
    IonHeader, 
    IonToolbar, 
    IonTitle, 
    IonContent,
    IonCard,
    IonCardHeader,
    IonCardTitle,
    IonCardContent,
    IonItem,
    IonLabel,
    IonInput,
    IonButton,
    IonSpinner,
    IonIcon,
    IonList
  ],
})
export class HomePage implements OnInit {
  postcode: string = '';
  houseNumber: string = '';
  binData: BinLookupResponse | null = null;
  loading: boolean = false;
  error: string = '';
  hasSearched: boolean = false;

  constructor(private binService: BinService) {
    addIcons({ trashOutline, locationOutline, refreshOutline });
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
}

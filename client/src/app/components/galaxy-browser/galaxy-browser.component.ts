import { Component, OnInit } from '@angular/core';
import { ImageItemDto, PredictResponseDto } from '../../interfaces/galaxy-interfaces';
import { GalaxyApiService } from '../../services/galaxy-api.service';
import { GalaxyResultCardComponent } from "../galaxy-result-card/galaxy-result-card.component";
import { ViewChild, ElementRef } from '@angular/core';

@Component({
  selector: 'app-galaxy-browser',
  templateUrl: './galaxy-browser.component.html',
  styleUrls: ['./galaxy-browser.component.css'],
  imports: [GalaxyResultCardComponent]
})
export class GalaxyBrowserComponent implements OnInit {
  images: ImageItemDto[] = [];
  selectedImage: ImageItemDto | null = null;
  uploadedFile: File | null = null;
  uploadedFileName = '';

  prediction: PredictResponseDto | null = null;
  gradCamUrl?:string;
  loadingImages = false;
  shufflingImages = false;
  predictingGalleryImage = false;
  uploadingAndPredicting = false;

  errorMessage = '';
  successMessage = '';

  constructor(private galaxyApiService: GalaxyApiService) {}

  ngOnInit(): void {
    this.loadRandomImages();
  }

  loadRandomImages(): void {
    this.loadingImages = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.galaxyApiService.getRandomImages().subscribe({
      next: (response) => {
        this.images = response.images ?? [];
        this.loadingImages = false;
      },
      error: (error) => {
        console.error('Failed to load random images', error);
        this.errorMessage = 'Failed to load images.';
        this.loadingImages = false;
      }
    });
  }

  shuffleImages(): void {
    this.shufflingImages = true;
    this.errorMessage = '';
    this.successMessage = '';
    this.prediction = null;
    this.selectedImage = null;

    this.galaxyApiService.shuffleImages().subscribe({
      next: (response) => {
        this.images = response.images ?? [];
        this.shufflingImages = false;
      },
      error: (error) => {
        console.error('Failed to shuffle images', error);
        this.errorMessage = 'Failed to shuffle images.';
        this.shufflingImages = false;
      }
    });
  }

  onGalleryImageClicked(image: ImageItemDto): void {
    this.selectedImage = image;
    this.prediction = null;
    this.errorMessage = '';
    this.successMessage = '';
  }

  predictSelectedImage(): void {
    if (!this.selectedImage?.s3Uri) {
      this.errorMessage = 'Please select an image first.';
      return;
    }

    this.predictingGalleryImage = true;
    this.errorMessage = '';
    this.successMessage = '';
    this.prediction = null;

    this.galaxyApiService.predictByS3Uri(this.selectedImage.s3Uri, 3).subscribe({
      next: (response) => {
        this.prediction = response;
        setTimeout(() => {
          window.scrollTo({
            top: document.documentElement.scrollHeight,
            behavior: 'smooth'
          });
        }, 100);
        this.gradCamUrl = `data:${response.gradCamMimeType};base64,${response.gradCamImageBase64}`;
        this.predictingGalleryImage = false;
        this.successMessage = 'Prediction completed.';
      },
      error: (error) => {
        console.error('Failed to predict selected image', error);
        this.errorMessage = 'Failed to predict selected image.';
        this.predictingGalleryImage = false;
      }
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files && input.files.length > 0 ? input.files[0] : null;
    this.uploadedFile = file;
    this.uploadedFileName = file?.name ?? '';
    this.errorMessage = '';
    this.successMessage = '';
  }

  uploadAndPredict(): void {
    if (!this.uploadedFile) {
      this.errorMessage = 'Please select a file to upload.';
      return;
    }

    this.uploadingAndPredicting = true;
    this.errorMessage = '';
    this.successMessage = '';
    this.prediction = null;
    this.selectedImage = null;

    this.galaxyApiService.uploadAndPredict(this.uploadedFile, 3).subscribe({
      next: (response) => {
        this.prediction = response;
        setTimeout(() => {
          window.scrollTo({
            top: document.documentElement.scrollHeight,
            behavior: 'smooth'
          });
        }, 100);
        this.uploadingAndPredicting = false;
        this.successMessage = 'Upload and prediction completed.';
      },
      error: (error) => {
        console.error('Failed to upload and predict image', error);
        this.errorMessage = 'Failed to upload and predict image.';
        this.uploadingAndPredicting = false;
      }
    });
  }

  trackByS3Uri(_: number, item: ImageItemDto): string {
    return item.s3Uri;
  }

  isSelected(image: ImageItemDto): boolean {
    return this.selectedImage?.s3Uri === image.s3Uri;
  }

  formatProbability(value: number | undefined | null): string {
    if (value === null || value === undefined) {
      return '0.00%';
    }

    return `${(value * 100).toFixed(2)}%`;
  }
}
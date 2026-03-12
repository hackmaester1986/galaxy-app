import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PredictByS3UriRequest, PredictResponseDto, RandomImagesResponseDto, UploadImageResponseDto } from '../interfaces/galaxy-interfaces';

@Injectable({
  providedIn: 'root'
})
export class GalaxyApiService {
  private readonly baseUrl = '/api';

  constructor(private http: HttpClient) {}

  getRandomImages(): Observable<RandomImagesResponseDto> {
    return this.http.get<RandomImagesResponseDto>(`${this.baseUrl}/images/random`);
  }

  shuffleImages(): Observable<RandomImagesResponseDto> {
    return this.http.post<RandomImagesResponseDto>(`${this.baseUrl}/images/shuffle`, {});
  }

  uploadImage(file: File): Observable<UploadImageResponseDto> {
    const formData = new FormData();
    formData.append('file', file);

    return this.http.post<UploadImageResponseDto>(`${this.baseUrl}/images/upload`, formData);
  }

  predictByS3Uri(s3Uri: string, topK: number = 3): Observable<PredictResponseDto> {
    const payload: PredictByS3UriRequest = {
      s3Uri,
      topK
    };

    return this.http.post<PredictResponseDto>(`${this.baseUrl}/predictions/by-s3-uri`, payload);
  }

  uploadAndPredict(file: File, topK: number = 3): Observable<PredictResponseDto> {
    const formData = new FormData();
    formData.append('file', file);

    return this.http.post<PredictResponseDto>(
      `${this.baseUrl}/predictions/upload?topK=${topK}`,
      formData
    );
  }
}
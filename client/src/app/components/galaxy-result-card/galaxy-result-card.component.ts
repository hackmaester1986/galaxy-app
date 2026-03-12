import { Component, Input } from '@angular/core';
import { PredictResponseDto, TopPredictionDto } from '../../interfaces/galaxy-interfaces';

@Component({
  selector: 'app-galaxy-result-card',
  imports: [],
  templateUrl: './galaxy-result-card.component.html',
  styleUrl: './galaxy-result-card.component.css'
})
export class GalaxyResultCardComponent {
  @Input({ required: true }) prediction!: PredictResponseDto;
  @Input() predictionType: 'res' | 'gal' = 'res';
  @Input() title = 'Prediction Result';

  get modelName(): string | undefined {
    return this.predictionType === 'res'
      ? this.prediction?.modelname_res
      : this.prediction?.modelname_gal;
  }

  get predictedLabel(): string {
    return this.predictionType === 'res'
      ? this.prediction?.predictedLabel_res ?? ''
      : this.prediction?.predictedLabel_gal ?? '';
  }

  get confidence(): number {
    return this.predictionType === 'res'
      ? this.prediction?.confidence_res ?? 0
      : this.prediction?.confidence_gal ?? 0;
  }

  get topPredictions(): TopPredictionDto[] {
    return this.predictionType === 'res'
      ? this.prediction?.topPredictions_res ?? []
      : this.prediction?.topPredictions_gal ?? [];
  }

  formatProbability(value?: number | null): string {
    if (value == null || Number.isNaN(value)) {
      return '0.00%';
    }

    return `${(value * 100).toFixed(2)}%`;
  }
}

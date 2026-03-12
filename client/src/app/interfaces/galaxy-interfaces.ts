export interface ImageItemDto {
  s3Uri: string;
  bucket: string;
  key: string;
  prefix: string;
  fileName: string;
  imageUrl: string;
}

export interface RandomImagesResponseDto {
  images: ImageItemDto[];
}

export interface UploadImageResponseDto {
  s3Uri: string;
  bucket: string;
  key: string;
  fileName: string;
}

export interface PredictByS3UriRequest {
  s3Uri: string;
  topK: number;
}

export interface TopPredictionDto {
  label: string;
  probability: number;
}

/*export interface PredictionStageDto {
  predictedLabel: string;
  confidence: number;
  probabilities: Record<string, number>;
  topPredictions: TopPredictionDto[];
}

export interface EnsemblePredictionDto {
  stage1: PredictionStageDto;
  stage2: PredictionStageDto;
  final: PredictionStageDto;
}

export interface PredictModelsDto {
  resnet: PredictionStageDto;
  galaxyCnn: PredictionStageDto;
  ensemble: EnsemblePredictionDto;
}*/

export interface PredictResponseDto {
  s3Uri?: string;

  modelname_res?: string;
  modelname_gal?: string;

  predictedLabel_res: string;
  confidence_res: number;
  probabilities_res: Record<string, number>;
  topPredictions_res: TopPredictionDto[];

  predictedLabel_gal: string;
  confidence_gal: number;
  probabilities_gal: Record<string, number>;
  topPredictions_gal: TopPredictionDto[];

  gradCamMimeType?: string;
  gradCamImageBase64?: string;
}
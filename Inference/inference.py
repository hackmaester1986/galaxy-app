import base64
import io
import logging
import os
from pathlib import Path
from typing import Any

import boto3
import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F
from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import JSONResponse
from PIL import Image
from pytorch_grad_cam import GradCAM
from pytorch_grad_cam.utils.image import show_cam_on_image
from pytorch_grad_cam.utils.model_targets import ClassifierOutputTarget
from torchvision import models, transforms
from urllib.parse import urlparse
from galaxycnn import GalaxyCNN
# -----------------------------------------------------------------------------
# Configuration
# -----------------------------------------------------------------------------

MODEL_DIR = Path(os.getenv("SM_MODEL_DIR", "/opt/ml/model"))
AWS_REGION = os.getenv("AWS_REGION", "us-east-1")
IMG_SIZE = int(os.getenv("IMG_SIZE", "224"))
TOP_K_DEFAULT = int(os.getenv("TOP_K_DEFAULT", "3"))

# Adjust these if your labels differ.
STAGE1_CLASS_NAMES = ["non_galaxy", "galaxy"]
STAGE2_CLASS_NAMES = ["elliptical", "disk"]

# -----------------------------------------------------------------------------
# Logging
# -----------------------------------------------------------------------------

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# -----------------------------------------------------------------------------
# App / Globals
# -----------------------------------------------------------------------------

app = FastAPI()
s3_client = boto3.client("s3", region_name=AWS_REGION)
MODEL_BUNDLE: dict[str, Any] | None = None




def make_resnet18(num_classes: int) -> nn.Module:
    model = models.resnet18(weights=None)
    model.fc = nn.Linear(model.fc.in_features, num_classes)
    return model


def make_galaxycnn(num_classes: int) -> nn.Module:
    return GalaxyCNN(num_classes=num_classes)


def load_checkpoint(path: Path) -> dict[str, Any]:
    if not path.exists():
        raise FileNotFoundError(f"Checkpoint not found: {path}")
    ckpt = torch.load(path, map_location="cpu", weights_only=False)
    if not isinstance(ckpt, dict):
        raise ValueError(f"Checkpoint at {path} is not a dict.")
    return ckpt


def load_resnet_checkpoint(path: Path, num_classes: int, device: torch.device) -> nn.Module:
    ckpt = load_checkpoint(path)
    model = make_resnet18(num_classes=num_classes)

    state = ckpt.get("model_state") or ckpt.get("model_state_dict")
    if state is None:
        raise KeyError(f"Checkpoint {path} is missing model_state/model_state_dict.")

    model.load_state_dict(state)
    model.to(device)
    model.eval()
    return model


def load_galaxycnn_checkpoint(path: Path, num_classes: int, device: torch.device) -> nn.Module:
    ckpt = load_checkpoint(path)
    model = make_galaxycnn(num_classes=num_classes)

    state = ckpt.get("model_state") or ckpt.get("model_state_dict")
    if state is None:
        raise KeyError(f"Checkpoint {path} is missing model_state/model_state_dict.")

    model.load_state_dict(state)
    model.to(device)
    model.eval()
    return model


def get_device() -> torch.device:
    return torch.device("cuda" if torch.cuda.is_available() else "cpu")


def parse_s3_uri(s3_uri: str) -> tuple[str, str]:
    parsed = urlparse(s3_uri)
    if parsed.scheme != "s3" or not parsed.netloc or not parsed.path:
        raise ValueError(f"Invalid s3_uri: {s3_uri}")
    return parsed.netloc, parsed.path.lstrip("/")


def load_image_from_s3(s3_uri: str) -> Image.Image:
    bucket, key = parse_s3_uri(s3_uri)
    logger.info("Downloading image from s3://%s/%s", bucket, key)
    response = s3_client.get_object(Bucket=bucket, Key=key)
    image_bytes = response["Body"].read()
    return Image.open(io.BytesIO(image_bytes)).convert("RGB")


def get_geom_transform() -> transforms.Compose:
    return transforms.Compose([
        transforms.Resize(256),
        transforms.CenterCrop(IMG_SIZE),
    ])


def get_model_transform() -> transforms.Compose:
    return transforms.Compose([
        transforms.ToTensor(),
        transforms.Normalize(
            mean=[0.485, 0.456, 0.406],
            std=[0.229, 0.224, 0.225],
        ),
    ])

def preprocess_image(
    image: Image.Image,
    geom_transform: transforms.Compose,
    model_transform: transforms.Compose,
) -> tuple[torch.Tensor, np.ndarray]:
    processed = geom_transform(image)

    rgb_uint8 = np.array(processed).astype(np.uint8)
    tensor = model_transform(processed).unsqueeze(0)

    return tensor, rgb_uint8


def probs_from_model(model: nn.Module, tensor: torch.Tensor) -> np.ndarray:
    with torch.no_grad():
        logits = model(tensor)
        probs = F.softmax(logits, dim=1).cpu().numpy()[0]
    return probs


def probability_map(class_names: list[str], probs: np.ndarray) -> dict[str, float]:
    return {class_names[i]: float(probs[i]) for i in range(len(class_names))}


def top_predictions(prob_map: dict[str, float], top_k: int) -> list[dict[str, float | str]]:
    ranked = sorted(prob_map.items(), key=lambda x: x[1], reverse=True)[:top_k]
    return [{"label": label, "probability": float(prob)} for label, prob in ranked]


def stage_result(class_names: list[str], probs: np.ndarray, top_k: int) -> dict[str, Any]:
    pred_idx = int(np.argmax(probs))
    pred_label = class_names[pred_idx]
    pred_conf = float(probs[pred_idx])
    prob_map = probability_map(class_names, probs)

    return {
        "predictedLabel": pred_label,
        "confidence": pred_conf,
        "probabilities": prob_map,
        "topPredictions": top_predictions(prob_map, top_k),
    }


def final_result_from_two_stage(stage1_probs: np.ndarray, stage2_probs: np.ndarray, top_k: int) -> dict[str, Any]:
    stage1_pred_idx = int(np.argmax(stage1_probs))
    stage1_label = STAGE1_CLASS_NAMES[stage1_pred_idx]
    stage1_prob_map = probability_map(STAGE1_CLASS_NAMES, stage1_probs)

    if stage1_label == "galaxy":
        stage2_pred_idx = int(np.argmax(stage2_probs))
        stage2_label = STAGE2_CLASS_NAMES[stage2_pred_idx]
        stage2_prob_map = probability_map(STAGE2_CLASS_NAMES, stage2_probs)
        return {
            "predictedLabel": stage2_label,
            "confidence": float(stage2_probs[stage2_pred_idx]),
            "probabilities": stage2_prob_map,
            "topPredictions": top_predictions(stage2_prob_map, top_k),
            "stage1Decision": {
                "predictedLabel": stage1_label,
                "confidence": float(stage1_probs[stage1_pred_idx]),
                "probabilities": stage1_prob_map,
            },
        }

    return {
        "predictedLabel": "non_galaxy",
        "confidence": float(stage1_probs[stage1_pred_idx]),
        "probabilities": stage1_prob_map,
        "topPredictions": top_predictions(stage1_prob_map, top_k),
        "stage1Decision": {
            "predictedLabel": stage1_label,
            "confidence": float(stage1_probs[stage1_pred_idx]),
            "probabilities": stage1_prob_map,
        },
    }


def averaged_probs(prob_a: np.ndarray, prob_b: np.ndarray) -> np.ndarray:
    return (prob_a + prob_b) / 2.0


def generate_gradcam_base64(
    model: nn.Module,
    target_layer: nn.Module,
    input_tensor: torch.Tensor,
    rgb_uint8: np.ndarray,
    target_class_idx: int,
) -> str:
    rgb_float = rgb_uint8.astype(np.float32) / 255.0

    with GradCAM(model=model, target_layers=[target_layer]) as cam:
        grayscale_cam = cam(
            input_tensor=input_tensor,
            targets=[ClassifierOutputTarget(target_class_idx)],
        )[0]

    visualization = show_cam_on_image(rgb_float, grayscale_cam, use_rgb=True)

    buffer = io.BytesIO()
    Image.fromarray(visualization).save(buffer, format="PNG")
    encoded = base64.b64encode(buffer.getvalue()).decode("utf-8")
    return encoded


def load_models() -> dict[str, Any]:
    device = get_device()
    geom_transform = get_geom_transform()
    model_transform = get_model_transform()

    logger.info("Loading models from %s", MODEL_DIR)
    logger.info("Model dir contents: %s", [p.name for p in MODEL_DIR.iterdir()])

    resnet_stage1 = load_resnet_checkpoint(MODEL_DIR / "resnet18_stage1_best.pt", 2, device)
    resnet_stage2 = load_resnet_checkpoint(MODEL_DIR / "resnet18_stage2_best.pt", 2, device)
    galaxycnn_stage1 = load_galaxycnn_checkpoint(MODEL_DIR / "galaxycnn_stage1_best.pt", 2, device)
    galaxycnn_stage2 = load_galaxycnn_checkpoint(MODEL_DIR / "galaxycnn_stage2_best.pt", 2, device)

    return {
        "device": device,
        "geom_transform": geom_transform,
        "model_transform": model_transform,
        "resnet": {
            "stage1": resnet_stage1,
            "stage2": resnet_stage2,
        },
        "galaxycnn": {
            "stage1": galaxycnn_stage1,
            "stage2": galaxycnn_stage2,
        },
    }


@app.on_event("startup")
async def startup_event() -> None:
    global MODEL_BUNDLE
    MODEL_BUNDLE = load_models()
    logger.info("Inference models loaded successfully.")


@app.get("/ping")
def ping() -> JSONResponse:
    if MODEL_BUNDLE is None:
        return JSONResponse(status_code=503, content={"status": "not_ready"})
    return JSONResponse(status_code=200, content={"status": "ok"})


@app.post("/invocations")
async def invocations(request: Request) -> JSONResponse:
    global MODEL_BUNDLE

    if MODEL_BUNDLE is None:
        raise HTTPException(status_code=503, detail="Models are not loaded.")

    content_type = request.headers.get("content-type", "")
    if "application/json" not in content_type:
        raise HTTPException(status_code=415, detail="Only application/json is supported.")

    try:
        payload = await request.json()
    except Exception as ex:
        raise HTTPException(status_code=400, detail=f"Invalid JSON body: {ex}") from ex

    s3_uri = payload.get("s3_uri") or payload.get("s3Uri") or payload.get("file_path") or payload.get("filePath")
    if not s3_uri:
        raise HTTPException(status_code=400, detail="Request body must include s3_uri.")

    top_k = int(payload.get("top_k", payload.get("topK", TOP_K_DEFAULT)))

    try:
        image = load_image_from_s3(s3_uri)
        input_tensor, rgb_uint8 = preprocess_image(
            image,
            MODEL_BUNDLE["geom_transform"],
            MODEL_BUNDLE["model_transform"],
        )
        input_tensor = input_tensor.to(MODEL_BUNDLE["device"])

        # ---------------------------------------------------------------------
        # Per-model stage predictions
        # ---------------------------------------------------------------------
        resnet_stage1_probs = probs_from_model(MODEL_BUNDLE["resnet"]["stage1"], input_tensor)
        resnet_stage2_probs = probs_from_model(MODEL_BUNDLE["resnet"]["stage2"], input_tensor)

        galaxycnn_stage1_probs = probs_from_model(MODEL_BUNDLE["galaxycnn"]["stage1"], input_tensor)
        galaxycnn_stage2_probs = probs_from_model(MODEL_BUNDLE["galaxycnn"]["stage2"], input_tensor)

        # ---------------------------------------------------------------------
        # Ensemble / combined output
        # ---------------------------------------------------------------------
        ensemble_final_res = final_result_from_two_stage(resnet_stage1_probs, resnet_stage2_probs, top_k)
        ensemble_final_gal = final_result_from_two_stage(galaxycnn_stage1_probs, galaxycnn_stage2_probs, top_k)
        # ---------------------------------------------------------------------
        # Grad-CAM
        #
        # If ensemble says galaxy, generate CAM from ResNet stage2 using the
        # ensemble's chosen stage2 class.
        # Otherwise generate CAM from ResNet stage1 using the non-galaxy/galaxy
        # class chosen by the ensemble.
        # ---------------------------------------------------------------------
        if ensemble_final_res["predictedLabel"] == "non_galaxy":
            cam_target_idx = int(np.argmax(resnet_stage1_probs))
            cam_model = MODEL_BUNDLE["resnet"]["stage1"]
            cam_layer = cam_model.layer4[-1]
        else:
            cam_target_idx = int(np.argmax(resnet_stage2_probs))
            cam_model = MODEL_BUNDLE["resnet"]["stage2"]
            cam_layer = cam_model.layer4[-1]

        gradcam_base64 = generate_gradcam_base64(
            model=cam_model,
            target_layer=cam_layer,
            input_tensor=input_tensor,
            rgb_uint8=rgb_uint8,
            target_class_idx=cam_target_idx,
        )

        # ---------------------------------------------------------------------
        # Top-level contract
        # ---------------------------------------------------------------------
        response = {
            "s3Uri": s3_uri,
            "modelname_res": "resnet18",
            "modelname_gal": "galaxycnn",
            "predictedLabel_res": ensemble_final_res["predictedLabel"],
            "confidence_res": ensemble_final_res["confidence"],
            "probabilities_res": ensemble_final_res["probabilities"],
            "topPredictions_res": ensemble_final_res["topPredictions"],
            "predictedLabel_gal": ensemble_final_gal["predictedLabel"],
            "confidence_gal": ensemble_final_gal["confidence"],
            "probabilities_gal": ensemble_final_gal["probabilities"],
            "topPredictions_gal": ensemble_final_gal["topPredictions"],
            "gradCamMimeType": "image/png",
            "gradCamImageBase64": gradcam_base64,
        }

        return JSONResponse(status_code=200, content=response)

    except HTTPException:
        raise
    except Exception as ex:
        logger.exception("Inference failed.")
        return JSONResponse(
            status_code=500,
            content={
                "error": str(ex),
                "s3Uri": s3_uri,
            },
        )
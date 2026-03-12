import sys
import uvicorn

# SageMaker passes "serve" when starting the container.
# Ignore it and just launch the app server.
if len(sys.argv) > 1 and sys.argv[1] == "serve":
    pass

uvicorn.run("inference:app", host="0.0.0.0", port=8080)
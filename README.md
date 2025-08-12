
# 3D Space Shooter RL Bot (DQN + Unity Live Inference)

## Overview
This project uses a DQN model to control a bot spaceship in Unity, based on live data from the player spaceship. The system runs in real-time using TCP communication.

## Components
- `PositionSender.cs`: Unity script for sending player & bot state to Python server.
- `BotController.cs`: Unity script to apply bot movement from Python model output.
- `server.py`: TCP server that receives state, runs model, returns prediction.
- `dqn_model.py`: MLP model for predicting bot movement.
- `train_dqn.py`: Simple training script with dummy data.

## Setup Instructions

### Unity
1. Attach `PositionSender.cs` to an empty GameObject.
   - Assign `playerRb` to your player spaceship Rigidbody.
   - Assign `botRb` to your bot spaceship Rigidbody.
2. Attach `BotController.cs` to your bot spaceship.

### Python
1. Install dependencies:
```bash
pip install torch numpy
```
2. Run the server:
```bash
python server.py
```

## Communication
Unity sends JSON over TCP every 100ms:
```json
{
  "player": {
    "position": {"x": ..., "y": ..., "z": ...},
    "velocity": {"x": ..., "y": ..., "z": ...},
    "rotation": {"x": ..., "y": ..., "z": ..., "w": ...}
  },
  "bot": { ... }
}
```

Python replies with:
```json
{
  "position": {...},
  "velocity": {...},
  "rotation": {...}
}
```

## Notes
- The model is untrained unless you train it using `train_dqn.py`
- Ensure the input feature size matches between Unity & model.

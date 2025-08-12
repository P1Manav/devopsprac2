# dqn_server.py
import socket, threading, json, time, os, random, math
import numpy as np
from collections import deque
import torch, torch.nn as nn, torch.optim as optim

# ---------------- config ----------------
HOST = "0.0.0.0"
PORT = 5000

STATE_SIZE = 18
YAW_OPTIONS   = [-6.0, 0.0, 6.0]
PITCH_OPTIONS = [-4.0, 0.0, 4.0]
ROLL_OPTIONS  = [-3.0, 0.0, 3.0]
ROT_COMBOS = [(y, p, r) for y in YAW_OPTIONS for p in PITCH_OPTIONS for r in ROLL_OPTIONS]
NUM_ROT = len(ROT_COMBOS)
ACTION_SIZE = NUM_ROT * 2

GAMMA = 0.99
LR = 1e-3
BATCH_SIZE = 64
MEMORY_SIZE = 30000
TARGET_UPDATE_EVERY = 200
SAVE_EVERY = 2000           # checkpoint interval
SAVE_EVERY_SMALL = 400      # save latest more frequently
MODEL_DIR = "checkpoints"
LATEST_MODEL = "bot_brain_latest.pth"

EPSILON = 1.0
EPSILON_MIN = 0.05
EPSILON_DECAY = 0.9995  # slower decay

# normalization factor for positions/velocities (adjust to your world scale)
NORM_POS = 100.0
NORM_VEL = 20.0

device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

# ---------------- model ----------------
class DQN(nn.Module):
    def __init__(self, in_dim, out_dim):
        super().__init__()
        self.net = nn.Sequential(
            nn.Linear(in_dim, 256),
            nn.ReLU(),
            nn.Linear(256, 128),
            nn.ReLU(),
            nn.Linear(128, out_dim)
        )
    def forward(self, x): return self.net(x)

policy_net = DQN(STATE_SIZE, ACTION_SIZE).to(device)
target_net = DQN(STATE_SIZE, ACTION_SIZE).to(device)
target_net.load_state_dict(policy_net.state_dict())
optimizer = optim.Adam(policy_net.parameters(), lr=LR)
loss_fn = nn.MSELoss()

if not os.path.exists(MODEL_DIR): os.makedirs(MODEL_DIR, exist_ok=True)

# load latest if exists
if os.path.exists(LATEST_MODEL):
    try:
        policy_net.load_state_dict(torch.load(LATEST_MODEL, map_location=device))
        target_net.load_state_dict(policy_net.state_dict())
        print("[DQN] Loaded latest model:", LATEST_MODEL)
    except Exception as e:
        print("[DQN] Failed loading latest model:", e)

# ---------------- memory ----------------
memory = deque(maxlen=MEMORY_SIZE)
memory_lock = threading.Lock()
model_lock = threading.Lock()
train_steps = 0
agents = {}  # agent_id -> {'prev_state', 'prev_action_idx', 'prev_dist'}

# ---------------- helpers ----------------
def encode_action(idx):
    shoot_idx = idx // NUM_ROT
    rot_idx = idx % NUM_ROT
    y,p,r = ROT_COMBOS[rot_idx]
    return float(y), float(p), float(r), bool(shoot_idx)

def normalize_state(player_pos, player_vel, player_rot, bot_pos, bot_vel, bot_rot):
    # scale positions and velocities to keep network inputs reasonable
    pv = np.array(player_pos, dtype=np.float32) / NORM_POS
    vv = np.array(player_vel, dtype=np.float32) / NORM_VEL
    pr = np.array(player_rot, dtype=np.float32) / 180.0  # angles -> -1..1
    bv = np.array(bot_pos, dtype=np.float32) / NORM_POS
    bvv = np.array(bot_vel, dtype=np.float32) / NORM_VEL
    br = np.array(bot_rot, dtype=np.float32) / 180.0
    return np.concatenate([pv, vv, pr, bv, bvv, br]).astype(np.float32)

def choose_action(state, player_in_fov=False, close=False):
    global EPSILON
    if random.random() < EPSILON:
        # biased exploration
        if player_in_fov and close:
            shoot_idx = 1 if random.random() < 0.95 else 0
        elif player_in_fov:
            shoot_idx = 1 if random.random() < 0.8 else 0
        else:
            shoot_idx = 1 if random.random() < 0.45 else 0
        rot_idx = random.randrange(NUM_ROT)
        return shoot_idx * NUM_ROT + rot_idx
    with model_lock:
        state_t = torch.tensor(state, dtype=torch.float32, device=device).unsqueeze(0)
        q = policy_net(state_t)
        return int(torch.argmax(q, dim=1).item())

def remember(s,a,r,s_next,done):
    with memory_lock:
        memory.append((s,a,r,s_next,done))

def replay():
    global train_steps
    with memory_lock:
        if len(memory) < BATCH_SIZE: return
        batch = random.sample(memory, BATCH_SIZE)
    states = torch.tensor(np.array([b[0] for b in batch]), dtype=torch.float32, device=device)
    actions = torch.tensor([b[1] for b in batch], dtype=torch.long, device=device).unsqueeze(1)
    rewards = torch.tensor([b[2] for b in batch], dtype=torch.float32, device=device).unsqueeze(1)
    next_states = torch.tensor(np.array([b[3] for b in batch]), dtype=torch.float32, device=device)
    dones = torch.tensor([1.0 if b[4] else 0.0 for b in batch], dtype=torch.float32, device=device).unsqueeze(1)

    with model_lock:
        q_values = policy_net(states).gather(1, actions)
        with torch.no_grad():
            next_q = target_net(next_states).max(1)[0].unsqueeze(1)
            target = rewards + (1.0 - dones) * GAMMA * next_q
        loss = loss_fn(q_values, target)
        optimizer.zero_grad(); loss.backward(); optimizer.step()

    train_steps += 1
    if train_steps % TARGET_UPDATE_EVERY == 0:
        with model_lock:
            target_net.load_state_dict(policy_net.state_dict())
            print(f"[DQN] Target synced at {train_steps}")
    if train_steps % SAVE_EVERY_SMALL == 0:
        try:
            torch.save(policy_net.state_dict(), LATEST_MODEL)
            print(f"[DQN] Saved latest model at {train_steps}")
        except Exception as e:
            print("[DQN] Save latest failed:", e)
    if train_steps % SAVE_EVERY == 0:
        try:
            fname = os.path.join(MODEL_DIR, f"bot_brain_step_{train_steps}.pth")
            torch.save(policy_net.state_dict(), fname)
            print(f"[DQN] Checkpoint saved: {fname}")
        except Exception as e:
            print("[DQN] Checkpoint save failed:", e)

def bot_forward_from_rot(rot):
    pitch_rad = math.radians(rot[0])
    yaw_rad = math.radians(rot[1])
    fx = math.cos(pitch_rad) * math.sin(yaw_rad)
    fy = -math.sin(pitch_rad)
    fz = math.cos(pitch_rad) * math.cos(yaw_rad)
    return np.array([fx, fy, fz], dtype=np.float32)

# ---------------- network handler ----------------
def handle_client(conn, addr):
    global EPSILON
    print("[NET] Connected:", addr)
    conn.settimeout(1.0)
    buffer = ""
    try:
        while True:
            try:
                data = conn.recv(8192)
            except socket.timeout:
                data = b""
            if data:
                buffer += data.decode("utf-8")

            while "\n" in buffer:
                line, buffer = buffer.split("\n", 1)
                if not line.strip(): continue
                try:
                    msg = json.loads(line)
                except Exception as e:
                    print("[NET] JSON parse error:", e); continue

                # HIT messages
                if "hit" in msg:
                    shooter = msg.get("shooter_id", None)
                    victim_agent = msg.get("victim_agent", None)
                    # credit shooter if it's a bot
                    if shooter and shooter.startswith("bot"):
                        ad = agents.get(shooter)
                        if ad and ad.get('prev_state') is not None:
                            print(f"[DQN] Bot {shooter} HIT -> +100")
                            remember(ad['prev_state'], ad['prev_action_idx'], 100.0, ad['prev_state'], True)
                            replay()
                    # penalize bot if it was hit by player
                    if victim_agent and victim_agent.startswith("bot") and shooter and shooter.startswith("player"):
                        vd = agents.get(victim_agent)
                        if vd and vd.get('prev_state') is not None:
                            print(f"[DQN] Bot {victim_agent} was HIT by player -> -100")
                            remember(vd['prev_state'], vd['prev_action_idx'], -100.0, vd['prev_state'], True)
                            replay()
                    # legacy: {"agent_id":"bot_1","hit": True}
                    if "agent_id" in msg and msg.get("agent_id","").startswith("bot"):
                        t = msg.get("agent_id")
                        if msg.get("hit", False):
                            ad = agents.get(t)
                            if ad and ad.get('prev_state') is not None:
                                remember(ad['prev_state'], ad['prev_action_idx'], 100.0, ad['prev_state'], True)
                                replay()
                    continue

                # STATE message
                agent_id = msg.get("agent_id", "bot_1")
                required = ("player_pos","player_vel","player_rot","bot_pos","bot_vel","bot_rot")
                if not all(k in msg for k in required):
                    print("[NET] State message missing keys:", msg.keys()); continue

                player_pos = np.array(msg["player_pos"], dtype=np.float32)
                player_vel = np.array(msg["player_vel"], dtype=np.float32)
                player_rot = np.array(msg["player_rot"], dtype=np.float32)
                bot_pos = np.array(msg["bot_pos"], dtype=np.float32)
                bot_vel = np.array(msg["bot_vel"], dtype=np.float32)
                bot_rot = np.array(msg["bot_rot"], dtype=np.float32)

                # normalized state
                state = normalize_state(player_pos, player_vel, player_rot, bot_pos, bot_vel, bot_rot)

                # geometry
                dir_vec = player_pos - bot_pos
                dist = float(np.linalg.norm(dir_vec) + 1e-8)
                dir_norm = dir_vec / (dist + 1e-8)
                bot_fwd = bot_forward_from_rot(bot_rot)
                align = float(np.dot(bot_fwd, dir_norm))  # -1..1

                # define conditions
                player_in_fov = align > math.cos(math.radians(25.0))
                close = dist <= 120.0

                # choose action
                action_idx = choose_action(state, player_in_fov, close)
                yaw_delta, pitch_delta, roll_delta, shoot_bool = encode_action(action_idx)

                # reward shaping
                reward = -0.02  # step penalty
                reward += align * 2.0     # reward facing player (scale up)
                if close and player_in_fov:
                    reward += 5.0          # bonus when close + aiming

                # distance change reward
                agent_prev = agents.get(agent_id, {})
                prev_dist = agent_prev.get('prev_dist', None)
                if prev_dist is not None:
                    reward += max(-1.0, (prev_dist - dist) * 0.8)

                # store previous transition
                if agent_prev.get('prev_state') is not None and agent_prev.get('prev_action_idx') is not None:
                    remember(agent_prev['prev_state'], agent_prev['prev_action_idx'], reward, state, False)
                    replay()

                # update agent tracking
                agents[agent_id] = {'prev_state': state, 'prev_action_idx': action_idx, 'prev_dist': dist}

                # epsilon decay
                if EPSILON > EPSILON_MIN:
                    EPSILON = max(EPSILON_MIN, EPSILON * EPSILON_DECAY)

                # respond
                resp = {"yaw_delta": yaw_delta, "pitch_delta": pitch_delta, "roll_delta": roll_delta, "shoot": shoot_bool}
                try:
                    conn.sendall((json.dumps(resp) + "\n").encode("utf-8"))
                except Exception as e:
                    print("[NET] send failed", e)
    except Exception as e:
        print("[NET] client handler exception:", e)
    finally:
        try: conn.close()
        except: pass
        print("[NET] Disconnected", addr)

# ---------------- server ----------------
def start_server():
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    sock.bind((HOST, PORT))
    sock.listen(8)
    print(f"[DQN] listening on {HOST}:{PORT}")
    while True:
        conn, addr = sock.accept()
        threading.Thread(target=handle_client, args=(conn, addr), daemon=True).start()

if __name__ == "__main__":
    start_server()

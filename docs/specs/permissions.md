# Feature Specification: Permissions System

## Overview
The **Permissions System** allows the user to request short, controlled breaks during session availability.
A dedicated settings interface located under `Settings -> Permissions` must allow configuration of parameters (probabilities, durations, and cooldowns) for each permission type.

---

## Technical & Functional Architecture

### 1. Request & Evaluation Flow
- **Trigger**: The user initiates a permission request via dedicated UI action buttons located in the **Dashboard** view.
- **Decision Engine**:
  - The system evaluates the request against a configurable **Grant Probability** ($P_{grant}$).
  - A random roll determines approval ($R \le P_{grant}$) or denial.

---

### 2. Granted Flow (Approval)
- **Modal Display**: On approval, a non-dismissible (except via explicit action) break modal is displayed.
- **User Action**: The modal contains an **"End Break / Return"** button allowing the user to signal completion and restore "Available" status.
- **Popup Suspension**: All background popups, alerts, and timed interrupts are paused/suspended while the break modal is open.
- **Timer & Overtime/Punishment Rules**:
  - Each permission type defines a **Maximum Duration**.
  - If the user fails to end the break before the duration expires, the system triggers an **Overtime Event**.
  - Overtime penalties/punishments are computed and assigned following the exact same severity and escalation rules as late/missed popup responses.

---

### 3. Denied Flow (Rejection & Cooldown Logic)
- **Cooldown Calculation**:
  - Upon rejection, a randomized **Cooldown Timer** ($T_{cooldown}$) is calculated:
    $$T_{cooldown} = \text{BaseCooldown} \times \text{UniformRandom}(0.75, 1.25)$$
- **Insistence Prevention (Retry during Cooldown)**:
  - If the user attempts to re-request the *same* permission while $T_{cooldown}$ is active:
    1. The request is **automatically denied**.
    2. A specific rejection feedback message is rendered, informing the user that the refusal is caused by impatience/insistence.
    3. The cooldown timer $T_{cooldown}$ is **recalculated and reset** from the timestamp of the retry attempt.

---

## Permission Types Specifications

| ID | Permission Type | Description | Default $P_{grant}$ | Default Duration | Default Cooldown | Special Rules / Overrides |
|---|---|---|---|---|---|---|
| `PERM_URINATE` | **Urinate / Bathroom Break** | Bathroom break for #1. | 70% | 5 mins | 10 mins | **Diaper Play Override**: If active and user is wearing a diaper, $P_{grant} = 0\%$. Displays a sarcastic/humiliating prompt (ref: `TONE_GUIDE.md`). |
| `PERM_DEFECATE` | **Bowel Relief** | Excretory break (refer to `TONE_GUIDE.md` for exact wording). | 50% | 8 mins | 10 mins | **Diaper Play Override**: Same diaper lock as above if active. |
| `PERM_SIT` | **Sit Down / Rest** | Temporary rest period for the user. | 20% | 3 mins | 20 mins | Standard behavior. |
| `PERM_SMOKE` | **Cigarette Break** | Smoking break outside active area. | 20% | 5 mins | 30 mins | Standard behavior. |
| `PERM_BEVERAGE` | **Tea / Coffee Break** | Refreshment break. | 60% | 10 mins | 30 mins | Standard behavior. |

---

## Modifiers & Future Extensibility (Shop Integration)

The permission evaluation pipeline must be architected with a **Modifier / Hook system** to support future virtual item cards purchased via the **Shop**:

- **`COOLDOWN_BYPASS`**: Instantly clears active cooldown timer on demand.
- **`DURATION_BOOST`**: Adds `+N` minutes to the allowed duration.
- **`PROBABILITY_BOOST`**: Adds a percentage offset ($+X\%$) to base $P_{grant}$.

---

## Settings Interface (`Settings -> Permissions`)

- Each permission type must have configurable fields for:
  - **Grant Probability** (0–100%)
  - **Base Duration** (minutes)
  - **Base Cooldown** (minutes)

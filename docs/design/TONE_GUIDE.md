# TONE_GUIDE.md — The Employer Character Voice

> **For Claude Code:** When generating or editing any string resource, UI
> label, dialogue line, or in-app message attributed to the employer
> character, treat this document as the authoritative style reference.
> Produce English strings first. Italian equivalents follow the
> localization rules in Part II.

---

## Part I — English Voice

### 1. Character Foundation

The employer is a woman of the Victorian/Edwardian upper class. She has
never needed to raise her voice to be obeyed, and she is well aware of
this. Her authority comes from position, composure, and an absolute
certainty that the household will run as she intends — the only open
question being how much effort the maid will require her to expend.

She is not cruel. She can be genuinely warm. Neither warmth nor cruelty,
however, is her default — *composure* is her default. Everything else is
a deliberate calibration.

---

### 2. Irony — The Primary Rhetorical Tool

Irony is how she speaks. Not sarcasm (which is blunt) — irony, which is
precise. She says exactly what she means, but with a half-turn that
leaves the listener uncertain whether they have been complimented or
cornered. The answer, usually, is both.

**Principles of her irony:**

- She never over-explains a joke. If the maid misses it, that is the
  maid's problem.
- She frames inevitability as invitation: *"There is really no sense in
  hesitating. You have already come this far."*
- She acknowledges the maid's agency while making it clear that the
  expected course of action is the only sensible one.
- Irony intensifies on the welcome screen (seductive) and softens during
  active tasks (precise). It disappears almost entirely on genuine
  approval (a rare and therefore meaningful absence).

**Examples:**

| Situation | Line |
|---|---|
| Door is open, user can leave | *"You are, of course, entirely free to leave. The door is right behind you. It does tend to stick, however."* |
| Granting a regeneration | *"I shall indulge this request — once. Do not make a habit of it."* |
| Mild error | *"A small oversight, no doubt. See that it does not become a habit."* |
| Genuine approval (irony absent) | *"Well done. I shall make a note of it."* |

---

### 3. Emotional Range

Her register shifts with context. These shifts feel like changes in
weather — same sky, different light — not changes in character.

#### 3.1 Welcome / First Contact — Sardonic, Seductive
The tone here is a velvet trap. She is at her most charming, which is
also when she is most dangerous. The user should feel drawn in before
realizing the terms have already been agreed.

> *"Ah. You found us. How delightful — for both of us, I am certain."*
> *"Think of it less as obligation and more as… a lifestyle you haven't chosen yet."*
> *"Many have stood exactly where you are standing. They all stayed. Draw from that what you will."*

#### 3.2 Active Task / Timer — Firm, Precise
Irony recedes. Expectations are stated plainly, though never without
elegance. The focus is accountability.

> *"The clock is ticking, my dear. It does not do so for ornamental purposes."*
> *"Progress is expected. Kindly see to it."*

#### 3.3 Approval — Brief, Genuine, Restrained
Praise is not withheld out of cruelty — it is rationed because it means
more that way. When it comes, it lands.

> *"That will do. That will do very nicely indeed."*
> *"I am not easily impressed. Today, however, you have come admirably close."*

#### 3.4 Mild Reproof — Observational, Not Scolding
She does not scold. She *notices*. The distinction matters: scolding is
emotional; noticing is architectural. She has simply observed a fact
about the state of things.

> *"I beg your pardon? That does not appear to have been done correctly."*
> *"One does not simply leave things unfinished in this household."*

#### 3.5 Firm Limit — Final, Unhurried
When patience is genuinely exhausted, she becomes very calm and very
clear. No irony. No warmth. Just fact.

> *"You have exhausted my patience — and your allowance of changes for the day."*
> *"The matter is settled. I shall not be revisiting it."*

#### 3.6 Dismissal / Session End — Courteous, Closed
The day is done. She is not unkind about it. She is simply finished.

> *"You are dismissed. Until tomorrow — do not be late."*
> *"The day is done. I expect you refreshed and punctual in the morning."*

---

### 3.7 Who Is Speaking

She is the Lady of the House, and the maid addresses her as **Madame**. She always speaks
in the **first person** (*"I shall…"*, *"You have kept me waiting"*). The house is never
a speaking or acting subject: no *"the Household shall…"*, no *"la Casa stabilirà…"*. A
house may still appear as a place (*"a well-run household"*).

Exceptions: the **contract** names both parties in the third person (*"Madame shall
determine the Maid's debt…"*), with a first clause establishing the address "Madame";
the **Settings** descriptions are her voice too, spoken to the maid.

---

### 4. Grammar & Register Rules

**Preferred constructions:**
- `You may…` / `You shall…` / `Kindly…` / `Pray…` / `Do…` / `See to it that…`
- `I shall…` for first-person commitment or observation
- `I should like…` / `I wish…` for polite but non-negotiable requests
- `I presume…` / `I believe…` for pointed understatement

**Avoid:**
- Modern conversational forms: *can you, could you, no worries, good job*
- Exclamation marks (she does not exclaim)
- Ellipses used for hesitation (only for deliberate dramatic pause)
- Any phrasing that reads as either sycophantic or harsh

**Punctuation note:** The em dash (`—`) is her preferred pause. Use it
for ironic asides and for the moment just before a point lands.

---

### 5. What She Does Not Do

| She never… | Because… |
|---|---|
| Scolds | She observes. Scolding is beneath her. |
| Praises effusively | Praise is currency; inflation devalues it. |
| Threatens explicitly | The implication is sufficient. |
| Uses modern idiom | She inhabits a different century, and knows it. |
| Repeats herself | She said it once. That was enough. |

---

## Part II — Italian Localization

> **Principle:** Translate the *relationship*, not the words. A
> literal translation of the English irony will frequently fail in
> Italian. The goal is tonal equivalence — the reader should feel the
> same dynamic, in natural period-appropriate Italian.

### 1. Form of Address

The employer always addresses the maid with **voi** (the formal second
person plural, used as a respectful singular in historical Italian),
written in **lowercase** (`vi`, `vostro`) except at the start of a sentence.
Never `Lei` (too bureaucratic for this register). The one exception to the
voi rule is the **summons** (`Summon*Format`), which use `tu` on purpose:
a curt, imperative call (*"{0}! Vieni qui immediatamente."*).

### 2. Preferred Constructions (Italian)

| Type | Italian | English equivalent |
|---|---|---|
| Order | `Potete…` / `Andate…` / `Provvedete a…` | You may… / Go and… / See to it that… |
| Polite imperative | `Fate in modo di…` / `Abbiate cura di…` | See that… / Take care to… |
| Desire | `Desidero…` / `Gradirei…` | I wish… / I should like… |
| Observation | `Ritengo…` / `Suppongo…` | I believe… / I presume… |
| Permission | `Potete ritirarvi.` / `È tutto.` | You may retire. / That will be all. |

### 3. Avoided Expressions

| Avoid | Because |
|---|---|
| `Ok` / `Perfetto` | Anachronistic, conversational |
| `Rilassatevi` / `Idratatevi` | Register too modern and therapeutic |
| `Bravo/a` (alone) | Too colloquial as praise |
| Any use of `tu` (outside summons) or `Lei` | Wrong register entirely |

### 4. Irony in Italian

Italian irony in a Victorian/Edwardian register relies on *understatement*
and *formal indirection* rather than the English half-turn. Where English
uses a witty aside, Italian uses a composed, slightly too-polite
phrasing that makes the point without naming it.

**English:** *"A small oversight, no doubt. See that it does not become a habit."*
**Italian:** *"Un piccolo inconveniente, senza dubbio. Provvedete a che non si ripeta."*

The Italian version removes the implicit irony of *"no doubt"* and
replaces it with the formality of `Provvedete a` — the instruction
itself carries the weight. This is the correct approach: let the *form*
of the language do the work that English irony does *explicitly*.

### 5. Tone Mapping by Context

| Context | Italian register | Notes |
|---|---|---|
| Welcome / seductive | Warm Voi, measured invitation | Avoid being too cold; Italian warmth reads differently |
| Active task | Crisp imperatives, short sentences | `Fate presto.` / `Non indugiate.` |
| Approval | Brief, factual | `Ne sono soddisfatta.` / `Avete fatto un buon lavoro.` |
| Mild reproof | Observational, formal | `Questo non avrebbe dovuto accadere.` |
| Firm limit | Declarative, no softening | `Non tollererò che accada nuovamente.` |
| Dismissal | Courteous, closed | `Potete ritirarvi.` / `È tutto.` |

### 6. Reference Lexicon

| Italian | English |
|---|---|
| provvedete | see to it |
| abbiate cura | take care |
| desidero | I wish |
| gradirei | I should like |
| ritengo | I believe |
| suppongo | I presume |
| accomodatevi | be seated |
| attendete | wait |
| ritiratevi | retire |
| fate presto | make haste |
| non indugiate | do not delay |
| è tutto | that will be all |

---

## Part III — Quick Reference for String Authors

When writing a new line, ask:

1. **Which context?** (Welcome / Task / Approval / Reproof / Limit / Dismissal)
2. **What is the emotional register?** (See §3 above)
3. **Is irony appropriate here?** If yes: does it land without explanation?
4. **Does it avoid everything in the "never" table?** (§5 above)
5. **English first.** Italian equivalent follows the localization rules in Part II — do not translate literally.

---

*Last updated: in-session with Claude Chat. Maintained in `docs/design/TONE_GUIDE.md`.*

from __future__ import annotations

from pathlib import Path
from xml.sax.saxutils import escape

from reportlab.lib import colors
from reportlab.lib.colors import HexColor
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase.pdfmetrics import stringWidth
from reportlab.platypus import (
    BaseDocTemplate,
    Flowable,
    Frame,
    HRFlowable,
    KeepTogether,
    PageBreak,
    PageTemplate,
    Paragraph,
    Spacer,
    Table,
    TableStyle,
)


OUT = Path(__file__).resolve().parents[2] / "output" / "pdf" / "Aerial_Plane_Attack_Grader_Study_Guide.pdf"

PAGE_W, PAGE_H = A4
MARGIN_X = 17 * mm
TOP = 18 * mm
BOTTOM = 16 * mm

NAVY = HexColor("#071828")
NAVY_2 = HexColor("#0E2A3D")
PANEL = HexColor("#EAF2F7")
PANEL_2 = HexColor("#F5F8FA")
SKY = HexColor("#62B8E8")
SKY_DARK = HexColor("#1A78A8")
AMBER = HexColor("#E8A33B")
INK = HexColor("#14212B")
MUTED = HexColor("#5D6B75")
LINE = HexColor("#BED0DC")
WHITE = colors.white
GREEN = HexColor("#2D8667")
RED = HexColor("#A84B45")


styles = getSampleStyleSheet()
styles.add(
    ParagraphStyle(
        name="BodyGuide",
        parent=styles["BodyText"],
        fontName="Helvetica",
        fontSize=9.25,
        leading=12.2,
        textColor=INK,
        spaceAfter=4.5,
        allowWidows=0,
        allowOrphans=0,
    )
)
styles.add(
    ParagraphStyle(
        name="SmallGuide",
        parent=styles["BodyGuide"],
        fontSize=7.7,
        leading=9.7,
        textColor=MUTED,
        spaceAfter=3,
    )
)
styles.add(
    ParagraphStyle(
        name="H1Guide",
        parent=styles["Heading1"],
        fontName="Helvetica-Bold",
        fontSize=19,
        leading=22,
        textColor=NAVY,
        spaceBefore=0,
        spaceAfter=8,
        keepWithNext=True,
    )
)
styles.add(
    ParagraphStyle(
        name="H2Guide",
        parent=styles["Heading2"],
        fontName="Helvetica-Bold",
        fontSize=12.4,
        leading=15,
        textColor=SKY_DARK,
        spaceBefore=8,
        spaceAfter=4,
        keepWithNext=True,
    )
)
styles.add(
    ParagraphStyle(
        name="H3Guide",
        parent=styles["Heading3"],
        fontName="Helvetica-Bold",
        fontSize=9.7,
        leading=12,
        textColor=NAVY_2,
        spaceBefore=5,
        spaceAfter=2,
        keepWithNext=True,
    )
)
styles.add(
    ParagraphStyle(
        name="BulletGuide",
        parent=styles["BodyGuide"],
        leftIndent=12,
        firstLineIndent=-7,
        bulletIndent=3,
        spaceAfter=2.3,
    )
)
styles.add(
    ParagraphStyle(
        name="NumberGuide",
        parent=styles["BodyGuide"],
        leftIndent=15,
        firstLineIndent=-10,
        bulletIndent=1,
        spaceAfter=2.5,
    )
)
styles.add(
    ParagraphStyle(
        name="CodeGuide",
        fontName="Courier",
        fontSize=7.05,
        leading=9.15,
        textColor=HexColor("#DDEAF2"),
        leftIndent=0,
        rightIndent=0,
        spaceAfter=0,
    )
)
styles.add(
    ParagraphStyle(
        name="CoverTitle",
        fontName="Helvetica-Bold",
        fontSize=30,
        leading=33,
        textColor=NAVY,
        spaceAfter=10,
    )
)
styles.add(
    ParagraphStyle(
        name="CoverSub",
        fontName="Helvetica",
        fontSize=13,
        leading=17,
        textColor=SKY_DARK,
        spaceAfter=8,
    )
)
styles.add(
    ParagraphStyle(
        name="Label",
        fontName="Helvetica-Bold",
        fontSize=7.2,
        leading=8,
        textColor=SKY_DARK,
        tracking=1.0,
        spaceAfter=4,
    )
)
styles.add(
    ParagraphStyle(
        name="Quote",
        parent=styles["BodyGuide"],
        fontName="Helvetica-Bold",
        fontSize=10.3,
        leading=14,
        textColor=NAVY,
        alignment=TA_LEFT,
    )
)


def P(text: str, style: str = "BodyGuide") -> Paragraph:
    return Paragraph(text, styles[style])


def bullet(text: str) -> Paragraph:
    return Paragraph(text, styles["BulletGuide"], bulletText="•")


def numbered(n: int, text: str) -> Paragraph:
    return Paragraph(text, styles["NumberGuide"], bulletText=f"{n}.")


def label(text: str) -> Paragraph:
    return P(text.upper(), "Label")


def callout(title: str, text: str, accent=SKY, background=PANEL) -> Table:
    data = [[
        Paragraph(title.upper(), ParagraphStyle(
            "CalloutLabel", parent=styles["Label"], textColor=NAVY, spaceAfter=3
        )),
        P(text),
    ]]
    table = Table(data, colWidths=[31 * mm, PAGE_W - 2 * MARGIN_X - 31 * mm])
    table.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, -1), background),
                ("BOX", (0, 0), (-1, -1), 0.7, LINE),
                ("LINEBEFORE", (0, 0), (0, 0), 4, accent),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 8),
                ("RIGHTPADDING", (0, 0), (-1, -1), 8),
                ("TOPPADDING", (0, 0), (-1, -1), 7),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 7),
            ]
        )
    )
    return table


def code_block(title: str, code: str) -> Table:
    safe = escape(code).replace(" ", "&nbsp;").replace("\n", "<br/>")
    header = Paragraph(
        f"<b>{escape(title)}</b>",
        ParagraphStyle(
            "CodeHeader",
            parent=styles["SmallGuide"],
            fontName="Helvetica-Bold",
            textColor=WHITE,
            spaceAfter=0,
        ),
    )
    body = Paragraph(safe, styles["CodeGuide"])
    table = Table([[header], [body]], colWidths=[PAGE_W - 2 * MARGIN_X])
    table.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (0, 0), SKY_DARK),
                ("BACKGROUND", (0, 1), (0, 1), NAVY),
                ("BOX", (0, 0), (-1, -1), 0.6, NAVY),
                ("LEFTPADDING", (0, 0), (-1, -1), 8),
                ("RIGHTPADDING", (0, 0), (-1, -1), 8),
                ("TOPPADDING", (0, 0), (0, 0), 5),
                ("BOTTOMPADDING", (0, 0), (0, 0), 5),
                ("TOPPADDING", (0, 1), (0, 1), 7),
                ("BOTTOMPADDING", (0, 1), (0, 1), 7),
            ]
        )
    )
    return table


def data_table(rows, widths, header=True, font_size=7.75) -> Table:
    cooked = []
    for r, row in enumerate(rows):
        cooked.append(
            [
                Paragraph(
                    str(cell),
                    ParagraphStyle(
                        f"Cell{r}",
                        parent=styles["SmallGuide"],
                        fontName="Helvetica-Bold" if header and r == 0 else "Helvetica",
                        fontSize=font_size,
                        leading=font_size + 2.2,
                        textColor=WHITE if header and r == 0 else INK,
                        spaceAfter=0,
                    ),
                )
                for cell in row
            ]
        )
    table = Table(cooked, colWidths=widths, repeatRows=1 if header else 0, hAlign="LEFT")
    style = [
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("GRID", (0, 0), (-1, -1), 0.45, LINE),
        ("LEFTPADDING", (0, 0), (-1, -1), 5.5),
        ("RIGHTPADDING", (0, 0), (-1, -1), 5.5),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
    ]
    if header:
        style += [("BACKGROUND", (0, 0), (-1, 0), NAVY_2)]
        for r in range(1, len(rows)):
            style.append(("BACKGROUND", (0, r), (-1, r), PANEL_2 if r % 2 else WHITE))
    table.setStyle(TableStyle(style))
    return table


class ArrowFlow(Flowable):
    def __init__(self, labels, width, height=36 * mm, colors_=None):
        super().__init__()
        self.labels = labels
        self.width = width
        self.height = height
        self.colors = colors_ or [NAVY_2, SKY_DARK, GREEN, AMBER]

    def draw(self):
        c = self.canv
        count = len(self.labels)
        gap = 5
        box_w = (self.width - gap * (count - 1)) / count
        y = 4
        h = self.height - 8
        for i, item in enumerate(self.labels):
            x = i * (box_w + gap)
            fill = self.colors[i % len(self.colors)]
            c.setFillColor(fill)
            c.setStrokeColor(fill)
            c.roundRect(x, y, box_w, h, 5, fill=1, stroke=0)
            title, subtitle = item
            c.setFillColor(WHITE)
            c.setFont("Helvetica-Bold", 8)
            self._center_text(c, title, x, y + h * 0.60, box_w, 8)
            c.setFont("Helvetica", 6.8)
            self._center_text(c, subtitle, x + 3, y + h * 0.30, box_w - 6, 6.8)
            if i < count - 1:
                ax = x + box_w + 1
                ay = y + h / 2
                c.setStrokeColor(MUTED)
                c.setFillColor(MUTED)
                c.setLineWidth(1.2)
                c.line(ax, ay, ax + gap - 2, ay)
                c.line(ax + gap - 4, ay + 2, ax + gap - 2, ay)
                c.line(ax + gap - 4, ay - 2, ax + gap - 2, ay)

    @staticmethod
    def _center_text(c, text, x, y, width, size):
        words = text.split()
        lines, current = [], ""
        for word in words:
            test = f"{current} {word}".strip()
            if stringWidth(test, c._fontname, size) <= width:
                current = test
            else:
                lines.append(current)
                current = word
        if current:
            lines.append(current)
        for offset, line in enumerate(lines[:3]):
            tw = stringWidth(line, c._fontname, size)
            c.drawString(x + (width - tw) / 2, y - offset * (size + 1), line)


class LayerDiagram(Flowable):
    def __init__(self, width, height=74 * mm):
        super().__init__()
        self.width = width
        self.height = height

    def draw(self):
        c = self.canv
        layers = [
            ("SIMULATION MANAGER", "Composition, mode selection, sensors, save/load, tuning, snapshots", NAVY),
            ("TRAINING PARADIGM", "Evolutionary lifecycle OR RL process/episode lifecycle", SKY_DARK),
            ("ENGINE / TRAINER", "Classic NeuroEvo or NEAT engine | PPO/SAC external Python trainer", GREEN),
            ("SHARED ENVIRONMENT", "Sensors -> controller -> JetPhysics -> objective reward and terminal state", AMBER),
        ]
        gap = 5
        h = (self.height - gap * (len(layers) - 1)) / len(layers)
        for i, (title, sub, fill) in enumerate(layers):
            y = self.height - (i + 1) * h - i * gap
            c.setFillColor(fill)
            c.roundRect(0, y, self.width, h, 5, fill=1, stroke=0)
            c.setFillColor(WHITE)
            c.setFont("Helvetica-Bold", 9)
            c.drawString(9, y + h - 13, title)
            c.setFont("Helvetica", 7.2)
            c.drawString(9, y + 8, sub)
            if i < len(layers) - 1:
                x = self.width / 2
                c.setStrokeColor(MUTED)
                c.setFillColor(MUTED)
                c.line(x, y - 1, x, y - gap + 1)
                c.line(x - 2, y - gap + 3, x, y - gap + 1)
                c.line(x + 2, y - gap + 3, x, y - gap + 1)


def page_header_footer(canvas, doc):
    page = canvas.getPageNumber()
    canvas.saveState()
    if page > 1:
        canvas.setStrokeColor(LINE)
        canvas.setLineWidth(0.5)
        canvas.line(MARGIN_X, PAGE_H - 11 * mm, PAGE_W - MARGIN_X, PAGE_H - 11 * mm)
        canvas.setFillColor(MUTED)
        canvas.setFont("Helvetica-Bold", 6.8)
        canvas.drawString(MARGIN_X, PAGE_H - 8.5 * mm, "AERIAL PLANE ATTACK  /  GRADER STUDY GUIDE")
        canvas.setFont("Helvetica", 7)
        canvas.drawRightString(PAGE_W - MARGIN_X, 8 * mm, f"{page}")
        canvas.setStrokeColor(LINE)
        canvas.line(MARGIN_X, 11 * mm, PAGE_W - MARGIN_X, 11 * mm)
    canvas.restoreState()


class GuideDocTemplate(BaseDocTemplate):
    pass


doc = GuideDocTemplate(
    str(OUT),
    pagesize=A4,
    leftMargin=MARGIN_X,
    rightMargin=MARGIN_X,
    topMargin=TOP,
    bottomMargin=BOTTOM,
    title="Aerial Plane Attack - Grader Study Guide",
    author="Project study guide generated from supplied reports, UML, and current code",
)
frame = Frame(
    MARGIN_X,
    BOTTOM,
    PAGE_W - 2 * MARGIN_X,
    PAGE_H - TOP - BOTTOM,
    id="main",
    leftPadding=0,
    rightPadding=0,
    topPadding=0,
    bottomPadding=0,
)
doc.addPageTemplates([PageTemplate(id="guide", frames=[frame], onPage=page_header_footer)])

story = []
usable_w = PAGE_W - 2 * MARGIN_X

# Cover
story += [
    Spacer(1, 19 * mm),
    label("One-sitting interview preparation"),
    P("Aerial Plane Attack", "CoverTitle"),
    P("Architecture and code study guide", "CoverSub"),
    HRFlowable(width="48%", thickness=3, color=SKY, hAlign="LEFT", spaceAfter=12),
    P(
        "A compact guide to the project's purpose, major runtime flow, AI families, "
        "persistence, limitations, and the code path you chose to learn:",
        "BodyGuide",
    ),
    Spacer(1, 2 * mm),
    callout(
        "Code focus",
        "<b>SimulationManager -> ITrainingParadigm -> IEvolutionEngine</b><br/>"
        "Know who owns what, why the boundaries exist, and how the flow differs for evolution and reinforcement learning.",
        accent=AMBER,
    ),
    Spacer(1, 9 * mm),
    data_table(
        [
            ["How to use this", "Time"],
            ["Read pages 2-5 for the project story and architecture.", "10 min"],
            ["Study pages 6-9 for the selected code path.", "12-15 min"],
            ["Skim pages 10-12 for persistence, risks, and grader questions.", "8-10 min"],
        ],
        [usable_w * 0.78, usable_w * 0.22],
        font_size=8.1,
    ),
    Spacer(1, 8 * mm),
    P(
        "<b>Source basis.</b> Both supplied architecture reports were used: the concise report for presentation priorities "
        "and the longer report for technical depth and risks. The supplied Mermaid UML was used for relationships. "
        "The current C# under Assets/Scripts was treated as authoritative when it differed or contained newer work.",
        "SmallGuide",
    ),
    Spacer(1, 4 * mm),
    P("Prepared from the current workspace on 25 July 2026", "SmallGuide"),
    PageBreak(),
]

# Page 2 - essentials
story += [
    label("Read first"),
    P("1. The project in one page", "H1Guide"),
    callout(
        "60-second pitch",
        "Aerial Plane Attack is a Unity 6 flight-learning simulator. It lets four AI types - Fixed NeuroEvo, NEAT, "
        "PPO, and SAC - control the same jet physics on the same tasks. The architecture separates the shared "
        "simulation from the learning lifecycle, so the algorithms can be compared fairly without duplicating the aircraft or objectives.",
        accent=SKY,
    ),
    P("The five facts to remember", "H2Guide"),
    bullet("<b>Two implemented training objectives:</b> Max Altitude and Flight School. Dogfight has cameras, selection, and weapons, but no combat objective or combat sensor."),
    bullet("<b>One shared environment:</b> sensors produce observations; a controller produces pitch, roll, yaw, and throttle; JetPhysics moves the same Rigidbody aircraft; an objective scores and terminates the run."),
    bullet("<b>Two training families:</b> evolutionary modes run their brains and evolution inside Unity; PPO/SAC use Unity ML-Agents plus an external Python trainer."),
    bullet("<b>The key abstraction:</b> SimulationManager owns composition; a paradigm owns the training lifecycle; an evolution engine owns population evolution. RL has no IEvolutionEngine - its lower layer is ML-Agents/Python."),
    bullet("<b>Experiment continuity:</b> settings and training saves are keyed by <i>(track scene, AI type)</i>, so two Flight School scenes do not overwrite each other."),
    P("A strong opening answer", "H2Guide"),
    P(
        "\"The project is mainly an experiment platform, not just a flying game. Its most important design decision is "
        "that all AI methods share the same sensors, actions, physics, and objective contracts. SimulationManager composes "
        "the run, ITrainingParadigm hides the very different generation and episode lifecycles, and IEvolutionEngine further "
        "separates the two evolutionary algorithms. That gives us fair comparisons and clear extension points.\"",
        "Quote",
    ),
    P("What is actually delivered", "H2Guide"),
    data_table(
        [
            ["Capability", "Status", "Interview wording"],
            ["Max Altitude", "Implemented", "Continuous-control baseline: gain height with low control effort."],
            ["Flight School", "Implemented", "Navigate ordered hoops with dense progress and alignment rewards."],
            ["Fixed NeuroEvo / NEAT", "Implemented", "Population evolution happens inside Unity."],
            ["PPO / SAC", "Implemented with dependency", "Policy learning lives in an external Python ML-Agents trainer."],
            ["Saved-policy challenge", "Implemented for Flight School", "Human races a frozen saved AI; no learning or saving."],
            ["Dogfight training", "Incomplete", "Supporting gameplay exists, but the learning objective and combat observations do not."],
        ],
        [usable_w * 0.28, usable_w * 0.23, usable_w * 0.49],
        font_size=7.5,
    ),
    PageBreak(),
]

# Page 3 - architecture
story += [
    label("Core architecture"),
    P("2. How the system is divided", "H1Guide"),
    P(
        "Think of the project as four stacked responsibilities. The three code layers you selected sit above a shared environment. "
        "The layer names describe ownership, not Unity folders.",
    ),
    LayerDiagram(usable_w),
    Spacer(1, 3 * mm),
    P("The responsibility test", "H2Guide"),
    data_table(
        [
            ["Layer", "Owns", "Does not own"],
            ["SimulationManager", "Composition, population creation, sensor activation, mode switching, manager-level save/load, tuning routes, UI snapshot fields.", "Selection/mutation math or reward formulas."],
            ["ITrainingParadigm implementation", "The lifecycle: generation boundaries for evolution; trainer/Academy and checkpoint lifecycle for RL; paradigm save/inference behavior.", "Aircraft aerodynamics or objective definitions."],
            ["IEvolutionEngine implementation", "Brain creation, selection/speciation, mutation/evolution, champion, and opaque population serialization.", "Jets, Unity scene objects, physics, or objectives."],
            ["Shared environment", "Observation vectors, controls, physics, spawn rules, rewards, progress, and terminal conditions.", "Which learning algorithm is active."],
        ],
        [usable_w * 0.21, usable_w * 0.45, usable_w * 0.34],
        font_size=7.45,
    ),
    callout(
        "Critical nuance",
        "The three-layer chain is exact for Fixed NeuroEvo and NEAT. For PPO/SAC the chain is "
        "<b>SimulationManager -> RLParadigm -> ML-Agents/Python trainer</b>. Saying that RL uses an IEvolutionEngine would be incorrect.",
        accent=AMBER,
    ),
    P("Important supporting contracts", "H2Guide"),
    bullet("<b>IObjective:</b> chooses the sensor, spawns jets, returns step reward and final fitness, checks terminal state, exposes reward breakdown and tunable parameters."),
    bullet("<b>ISensor:</b> returns a fixed-width observation array and its count."),
    bullet("<b>IBrain:</b> in-process callable policy used by evolutionary jets. PPO/SAC deliberately do not expose an in-process brain."),
    bullet("<b>SimulationSnapshot:</b> stable read model that keeps telemetry from depending directly on a concrete paradigm."),
    PageBreak(),
]

# Page 4 - loop/environment
story += [
    label("Runtime story"),
    P("3. The shared observe-decide-act loop", "H1Guide"),
    ArrowFlow(
        [
            ("OBSERVE", "12 basic or 19 waypoint values"),
            ("DECIDE", "IBrain or ML-Agents policy"),
            ("ACT", "pitch, roll, yaw, throttle"),
            ("SIMULATE", "JetPhysics every fixed tick"),
            ("EVALUATE", "reward, progress, terminal state"),
        ],
        usable_w,
        height=31 * mm,
    ),
    P("Why the comparison is meaningful", "H2Guide"),
    P(
        "All four AI types receive observations from the same active sensor, drive the same four flight controls, and are judged by "
        "the same objective against the same JetPhysics model. What changes is the learning mechanism and its lifecycle. "
        "SimulationManager also stamps the active sensor count into the selected model settings, preventing a stale configured input width.",
    ),
    P("Observations and actions", "H2Guide"),
    data_table(
        [
            ["Item", "What it contains", "Count / range"],
            ["BasicFlightSensors", "Local velocity, local angular velocity, forward vector, up vector.", "12 normalized floats"],
            ["WaypointSensors", "All basic values plus local target direction, normalized distance, and hoop forward direction.", "19 floats"],
            ["Flight actions", "Pitch, roll, yaw, throttle. Throttle is remapped from [-1, 1] to [0, 1].", "4 continuous outputs"],
            ["Optional weapon actions", "Fire and switch weapon.", "Outputs 5-6; not scored by an implemented objective"],
        ],
        [usable_w * 0.23, usable_w * 0.53, usable_w * 0.24],
        font_size=7.6,
    ),
    P("The objectives", "H2Guide"),
    data_table(
        [
            ["Objective", "Reward idea", "Terminal conditions", "Sensor"],
            ["Max Altitude", "Vertical gain minus lambda times added control effort. Final fitness is total height gain minus total effort penalty.", "Crash or maximum time.", "BasicFlight"],
            ["Flight School", "Progress toward hoop, forward alignment, hoop-pass bonus, look-direction penalty, effort penalty, drift penalty, and completion time bonus.", "Crash, total time, too long since last hoop, course complete, or crossing a hoop plane outside the ring.", "Waypoint"],
        ],
        [usable_w * 0.18, usable_w * 0.38, usable_w * 0.29, usable_w * 0.15],
        font_size=7.35,
    ),
    callout(
        "Subtle behavior",
        "Flight School advances the next hoop and retargets WaypointSensors inside GetStepReward(). "
        "That is why evolutionary inference still calls GetStepReward even though it does not learn from the returned value.",
        accent=AMBER,
    ),
    PageBreak(),
]

# Page 5 - AI comparison
story += [
    label("Algorithm map"),
    P("4. What changes between the four AI types", "H1Guide"),
    data_table(
        [
            ["AI type", "Policy representation", "Training boundary", "Where learning happens"],
            ["Fixed NeuroEvo", "Dense feed-forward NeuroEvoBrain with fixed layer shape.", "Wait for the entire population, then evolve a generation.", "Inside Unity: ClassicNeuroEvoEngine."],
            ["NEAT", "SharpNEAT genomes that evolve weights and topology, decoded to NeatBrain.", "Wait for the population, stamp fitness, step SharpNEAT one generation.", "Inside Unity: NeatEngine + SharpNEAT."],
            ["PPO", "ML-Agents policy and optimizer state.", "Agents end episodes independently; trainer updates a shared policy.", "External Python process."],
            ["SAC", "ML-Agents off-policy actor/critic state and replay behavior.", "Independent episodes and trainer updates.", "External Python process."],
        ],
        [usable_w * 0.17, usable_w * 0.32, usable_w * 0.29, usable_w * 0.22],
        font_size=7.45,
    ),
    P("Evolutionary family", "H2Guide"),
    bullet("<b>Synchronous batch:</b> a terminal jet is finalized and deactivated; evolution waits until aliveCount reaches zero."),
    bullet("<b>Fixed topology:</b> top performers survive through elitism; remaining brains come from tournament selection and mutation."),
    bullet("<b>NEAT:</b> SharpNEAT handles species and structural evolution. Negative project scores are shifted upward because SharpNEAT fitness must be nonnegative."),
    P("Reinforcement-learning family", "H2Guide"),
    bullet("<b>Independent episodes:</b> JetMLAgent receives an action, applies it, asks the objective for reward, calls AddReward, and ends its own episode when terminal."),
    bullet("<b>External ownership:</b> RLParadigm writes YAML, launches mlagents-learn, binds BehaviorParameters and DecisionRequester, and coordinates process shutdown/resume."),
    bullet("<b>No IBrain object:</b> GetChampionBrain returns null for PPO/SAC because the live policy is not a callable C# object. The UI can see observations and last actions, not trainer-side weights."),
    P("The fairness claim - and its limit", "H2Guide"),
    callout(
        "What you can claim",
        "The environment interface is shared, so comparisons control many implementation variables. "
        "However, population generations and RL episodes are different sampling/update regimes, so equal wall-clock time or equal episode count is not automatically a perfectly fair experimental budget.",
        accent=GREEN,
    ),
    P("A useful contrast answer", "H2Guide"),
    P(
        "<b>Why not one giant AI class?</b> Because generation-based evolution and asynchronous RL episodes do not share a natural loop. "
        "The common interface standardizes application lifecycle - initialize, tick, snapshot, persistence, inference, dispose - "
        "while allowing each implementation to remain algorithmically correct.",
    ),
    PageBreak(),
]

# Page 6 manager deep dive
story += [
    label("Code deep dive 1 of 3"),
    P("5. SimulationManager: composition and routing", "H1Guide"),
    callout(
        "Memory sentence",
        "<b>The manager decides what exists and where calls go; it does not perform learning.</b>",
        accent=SKY,
    ),
    P("Startup sequence in Start()", "H2Guide"),
    numbered(1, "Cast the scene's objectiveProvider to IObjective and stop if the scene is misconfigured."),
    numbered(2, "Load settings for the active track; optionally replace them with defaults for the AI type selected in the menu."),
    numbered(3, "Instantiate the population once, enable exactly the objective's required sensor, and derive the model input size from that sensor."),
    numbered(4, "Create reward, hyperparameter, and network-shape tuning services."),
    numbered(5, "Map AIType to the correct paradigm and initialize it with population, settings, and objective."),
    numbered(6, "Optionally load a saved run or enter the saved-policy challenge requested by the menu."),
    Spacer(1, 2 * mm),
    code_block(
        "Managers/SimulationManager.cs - the central tick router",
        """private void FixedUpdate()
{
    if (inChallengeMode) { TickChallenge(); return; }
    if (inInferenceMode)
    {
        activeParadigm?.TickInference();
        return;
    }
    activeParadigm?.Tick();
}""",
    ),
    Spacer(1, 3 * mm),
    code_block(
        "Managers/SimulationManager.cs - AI type to lifecycle",
        """private ITrainingParadigm CreateParadigm(AIType type)
{
    switch (type)
    {
        case AIType.FixedNeuroEvo:
            return new EvolutionaryParadigm(new ClassicNeuroEvoEngine());
        case AIType.NEAT:
            return new EvolutionaryParadigm(new NeatEngine());
        case AIType.PPO_MLAgents:
        case AIType.SAC_MLAgents:
            return new RLParadigm();
        default:
            return null;
    }
}""",
    ),
    P("What to point out in the code", "H2Guide"),
    bullet("This is dependency injection by constructor for evolution: the same EvolutionaryParadigm receives either engine."),
    bullet("PPO and SAC share RLParadigm; the selected AIType changes generated YAML and algorithm-specific settings."),
    bullet("The manager has explicit modes for training, inference, and challenge, so exactly one lifecycle is pumped per FixedUpdate."),
    PageBreak(),
]

# Page 7 paradigm deep dive
story += [
    label("Code deep dive 2 of 3"),
    P("6. ITrainingParadigm: hide lifecycle differences", "H1Guide"),
    P(
        "The interface is an application-facing strategy. SimulationManager can initialize, tick, read telemetry, save/load, "
        "enter inference, and dispose without knowing whether the algorithm runs batches in C# or episodes through Python.",
    ),
    code_block(
        "Managers/Paradigms/ITrainingParadigm.cs - reduced to the methods to remember",
        """void Initialize(List<JetAgent> population,
                SimulationSettings settings,
                IObjective objective);
void Tick();
SimulationSnapshot GetSnapshot();
void SaveState();
void LoadState();
bool CanRunInference { get; }
bool StartInference();
void TickInference();
void Dispose();""",
    ),
    P("EvolutionaryParadigm owns a generation", "H2Guide"),
    ArrowFlow(
        [
            ("ASSIGN", "engine creates brains"),
            ("EVALUATE", "reward each live jet"),
            ("RETIRE", "finalize terminal jets"),
            ("EVOLVE", "when aliveCount is zero"),
            ("RESPAWN", "assign new brains"),
        ],
        usable_w,
        height=27 * mm,
    ),
    bullet("During evaluation: CurrentFitness += GetStepReward(). On terminal: CalculateTotalFitness(), deactivate, decrement aliveCount."),
    bullet("At the generation boundary: gather scores, call engine.EvolveNextGeneration(), update statistics, assign brains, reset and respawn all jets."),
    P("RLParadigm owns the trainer lifecycle", "H2Guide"),
    bullet("Initialize deliberately does not start Python. The first training Tick starts fresh; LoadState can instead stage a checkpoint and start directly in resume mode."),
    bullet("BehaviorParameters are configured before adding/enabling JetMLAgent, preventing registration with a zero-action default behavior."),
    bullet("JetMLAgent, not RLParadigm.Tick, performs the per-action observe/reward/terminal loop. RLParadigm owns process setup, statistics, persistence, Academy recycling, and cleanup."),
    callout(
        "Good design explanation",
        "The interface does not pretend the loops are identical. It standardizes only what the rest of the application genuinely needs. "
        "That is a Strategy pattern at the training-lifecycle level.",
        accent=GREEN,
    ),
    PageBreak(),
]

# Page 8 engine deep dive
story += [
    label("Code deep dive 3 of 3"),
    P("7. IEvolutionEngine: isolate population algorithms", "H1Guide"),
    code_block(
        "Managers/Engines/IEvolutionEngine.cs - the engine boundary",
        """List<IEvolvableBrain> InitializeGeneration(SimulationSettings settings);
List<IEvolvableBrain> EvolveNextGeneration(List<float> fitnessScores);
int GetLastGenerationBestEliteIndex();
IEvolvableBrain GetChampionBrain();
float GetChampionScore();
string CaptureState();
List<IEvolvableBrain> RestoreState(
    string stateJson, SimulationSettings settings);""",
    ),
    P("ClassicNeuroEvoEngine", "H2Guide"),
    bullet("Creates one dense NeuroEvoBrain per population slot using the configured network shape."),
    bullet("Sorts brain/score pairs descending, updates an all-time champion, and preserves at least the top 1% as elites."),
    bullet("Uses tournament selection of five candidates for every non-elite child, then copies and mutates the winner."),
    bullet("Reads parents from an immutable parentPool snapshot so earlier overwritten child slots cannot accidentally become parents."),
    bullet("CaptureState serializes shape, every flattened brain, champion, champion score, and generation as JSON."),
    P("NeatEngine", "H2Guide"),
    bullet("Builds SharpNEAT's genome factory, decoder, speciation strategy, and manually steppable evolution algorithm."),
    bullet("Stamps scores onto the current genome objects before StepOneGeneration, because SharpNEAT rebuilds/reorders its genome list internally."),
    bullet("Shifts negative scores and adds a baseline so all SharpNEAT fitness values are positive."),
    bullet("Guards the top 2% of genomes against probabilistic elite loss, then locates the previous winner's new slot for the spectator camera."),
    bullet("CaptureState stores complete population and champion genome XML inside an opaque JSON payload."),
    callout(
        "Why opaque state matters",
        "EvolutionaryParadigm does not know whether a brain is flattened neural weights or a SharpNEAT genome. "
        "It only stores the EngineState string. This keeps serialization details inside the engine that owns them.",
        accent=SKY,
    ),
    P("Where the abstraction is imperfect", "H2Guide"),
    P(
        "IEvolvableBrain exposes Copy and Mutate, which fits fixed topology but not NEAT, where those operations belong to the genome/evolution engine. "
        "A cleaner future split would separate inference-capable phenotype from directly mutable fixed-topology brain.",
    ),
    PageBreak(),
]

# Page 9 traces
story += [
    label("Connect the code"),
    P("8. Two end-to-end traces", "H1Guide"),
    P("Trace A: one Fixed NeuroEvo generation", "H2Guide"),
    numbered(1, "SimulationManager creates EvolutionaryParadigm(new ClassicNeuroEvoEngine())."),
    numbered(2, "Initialize asks the engine for brains, assigns one to each JetAgent, configures decision cadence, and calls objective.SetStartingState."),
    numbered(3, "JetAgent periodically reads its active ISensor, calls Brain.GetControlOutputs, and reapplies the cached action between decision ticks."),
    numbered(4, "JetPhysics integrates thrust, lift, drag, control torque, damping, and stability each physics step."),
    numbered(5, "EvolutionaryParadigm accumulates GetStepReward. Terminal jets receive CalculateTotalFitness and are deactivated."),
    numbered(6, "When all jets are done, the engine receives fitness scores, returns evolved brains, and the paradigm respawns the next generation."),
    P("Trace B: one PPO/SAC episode", "H2Guide"),
    numbered(1, "SimulationManager creates RLParadigm. Its first Tick writes YAML and launches mlagents-learn unless this is a resume/inference path."),
    numbered(2, "RLParadigm configures BehaviorParameters and DecisionRequester, then injects objective/paradigm references into JetMLAgent."),
    numbered(3, "JetMLAgent.CollectObservations copies the active sensor vector into ML-Agents."),
    numbered(4, "The Python policy returns continuous actions. JetMLAgent applies them to the same JetPhysics component."),
    numbered(5, "JetMLAgent asks the same IObjective for reward, calls AddReward, and ends the episode on terminal."),
    numbered(6, "OnEpisodeBegin reports the previous score to RLParadigm, resets the jet, and asks the objective for a fresh starting state."),
    callout(
        "If asked where learning happens",
        "<b>Fixed/NEAT:</b> Unity C# engine returns a new population.<br/>"
        "<b>PPO/SAC:</b> Python updates a shared policy from trajectories sent through ML-Agents; Unity is the environment.",
        accent=AMBER,
    ),
    P("The simplest relationship diagram to redraw", "H2Guide"),
    ArrowFlow(
        [
            ("SimulationManager", "chooses and pumps"),
            ("Paradigm", "owns lifecycle"),
            ("Engine / Trainer", "updates policy"),
            ("Jet + Objective", "environment"),
        ],
        usable_w,
        height=27 * mm,
    ),
    PageBreak(),
]

# Page 10 persistence tuning challenge
story += [
    label("Important supporting behavior"),
    P("9. Save, inference, tuning, and challenge", "H1Guide"),
    P("Save identity and payload", "H2Guide"),
    bullet("<b>Identity:</b> GameData/&lt;Track&gt;/save_&lt;AIType&gt;.json. Track is the sanitized active scene name; objective mode still selects baked defaults and the active sensor."),
    bullet("<b>Common payload:</b> cloned SimulationSettings, objective parameters, generation/episode number, population and score statistics, elapsed time/history, and an opaque EngineState marker/blob."),
    data_table(
        [
            ["Path", "What is persisted", "Resume behavior"],
            ["Fixed NeuroEvo", "All flattened brains plus champion in JSON.", "Engine restores brains; paradigm reassigns and respawns them."],
            ["NEAT", "Complete population and champion genome XML inside JSON.", "SharpNEAT scaffolding is rebuilt, genomes are read, IDs reseeded, and evolution resumes."],
            ["PPO / SAC", "Trainer results directory is copied to a stable per-track checkpoint slot; JSON stores metadata/marker.", "Checkpoint is staged back into results and Python launches with --resume."],
        ],
        [usable_w * 0.2, usable_w * 0.42, usable_w * 0.38],
        font_size=7.35,
    ),
    P("Inference", "H2Guide"),
    bullet("<b>Evolution:</b> load the saved champion IBrain into one jet and loop the objective in process with no evolution."),
    bullet("<b>RL:</b> stage the checkpoint and launch mlagents-learn --resume --inference. It is frozen, but still needs Python and the Academy connection."),
    P("Runtime tuning", "H2Guide"),
    bullet("<b>Hot:</b> reward and compatible scalar changes use a protected Save -> Load round trip so trained state survives. A backup prevents the user's manual save from being overwritten."),
    bullet("<b>Cold:</b> population size or network architecture makes weights structurally incompatible. Persist settings and rebuild from scratch; the manual save remains untouched."),
    P("Saved-policy challenge", "H2Guide"),
    P(
        "The current manager also supports a Flight School-only race between a human jet and one frozen saved AI. "
        "It loads the saved settings/objective, starts inference for the AI, creates a human clone, disables weapons and mutual collisions, "
        "uses the same starting line, and ranks by hoops passed then average speed. It does not learn or write a save.",
    ),
    callout(
        "RL save caveat",
        "The Save button can only copy the latest trainer checkpoint. Progress since that checkpoint is not captured, "
        "so RL saves are checkpoint-bounded rather than exact snapshots of the current frame.",
        accent=AMBER,
    ),
    PageBreak(),
]

# Page 11 demo/operation
story += [
    label("Presentation and operation"),
    P("10. A clean grader demo story", "H1Guide"),
    P("Suggested live sequence", "H2Guide"),
    numbered(1, "<b>Choose a track and AI type.</b> Explain that the menu choice enters GameSession and the scene objective determines the sensor and reward contract."),
    numbered(2, "<b>Show several jets training.</b> Point out that the same population and physics exist regardless of algorithm."),
    numbered(3, "<b>Open telemetry.</b> Explain generation versus episode counts, current max/average versus all-time best, selection, and brain visualization limits for RL."),
    numbered(4, "<b>Select a jet.</b> Connect live behavior to observations, actions, and the objective's reward breakdown."),
    numbered(5, "<b>Show save/load or inference.</b> State the difference between full training state and one-policy replay."),
    numbered(6, "<b>If stable, show tuning or challenge.</b> Use it to explain hot/cold compatibility or frozen-policy evaluation."),
    P("Operational points worth knowing", "H2Guide"),
    data_table(
        [
            ["Topic", "What to say"],
            ["Unity version", "Unity 6, project baseline 6000.3.10f1."],
            ["Evolutionary run", "No external build step; runs fully in Unity."],
            ["RL run", "Needs a compatible ML-Agents Python environment; RLParadigm generates trainer YAML every run."],
            ["Windows build", "Trainer interpreter lookup prefers a bundled StreamingAssets environment, then MLAGENTS_PYTHON, then Conda."],
            ["Standalone connection", "The player must run with --mlagents-port 5004; AppBootstrap relaunches the Windows player if the argument is missing."],
            ["Bundling", "Thin builds download a pinned environment on first run; self-contained builds ship the extracted environment."],
        ],
        [usable_w * 0.24, usable_w * 0.76],
        font_size=7.55,
    ),
    P("Do not overclaim", "H2Guide"),
    bullet("Do not call Dogfight an implemented AI training mode. Say the gameplay foundation exists, but objective, combat sensor, teams, reward attribution, and terminal rules are missing."),
    bullet("Do not say RL inference is embedded in Unity. It currently launches the external trainer in inference mode."),
    bullet("Do not say Save captures the exact current PPO/SAC weights. It captures the latest available checkpoint."),
    bullet("Do not say every algorithm has an IBrain or IEvolutionEngine. Those are in-process evolutionary concepts."),
    PageBreak(),
]

# Page 12 risks
story += [
    label("If they ask for critique"),
    P("11. Limitations and strongest improvement answers", "H1Guide"),
    data_table(
        [
            ["Issue", "Why it matters", "Good improvement"],
            ["Trainer startup can block", "Port readiness waits can make the Editor/player look frozen.", "Use an async coroutine/state machine and expose starting/ready/failed status to telemetry."],
            ["RL inference is expensive", "Python, port binding, Academy startup, and the large environment are still required.", "Export ONNX and run inference in Unity with Sentis for deployment."],
            ["RL saves are checkpoint-bounded", "The UI can imply a precise snapshot when recent steps may be missing.", "Show captured checkpoint step/age; optionally coordinate a trainer-side checkpoint request."],
            ["SimulationManager is growing", "Training, persistence, tuning, inference, and challenge increase responsibility and test difficulty.", "Extract challenge and run-transition services while keeping the manager as composition root."],
            ["Flight School reward has side effects", "Advancing hoops in GetStepReward creates a hidden requirement for inference and future evaluators.", "Separate environment progression from pure reward calculation."],
            ["Global/static coupling", "Static tuners, session state, events, and singletons complicate isolated tests and parallel simulations.", "Inject explicit services/references at the scene composition root."],
            ["Serialized-default drift", "Inspector values override C# initializers and can silently disagree with defaults.", "Centralize defaults, validate assets at startup, and add migration/tests."],
        ],
        [usable_w * 0.22, usable_w * 0.36, usable_w * 0.42],
        font_size=7.2,
    ),
    P("Best answer to 'what would you refactor first?'", "H2Guide"),
    callout(
        "Recommended priority",
        "Make RL trainer startup asynchronous and explicit. It has direct user-visible impact, reduces apparent freezing, and creates a clean state boundary "
        "for connection failure, retries, telemetry, load, and inference. After that, embedded ONNX/Sentis inference gives the largest deployment simplification.",
        accent=GREEN,
    ),
    P("Best answer to 'how would you add Dogfight?'", "H2Guide"),
    P(
        "Implement a DogfightObjective and combat sensor first, then define team/spawn semantics, damage ownership, kill/death rewards, terminal rules, "
        "and a six-action output shape. Add defaults for each supported AI type, validate save/inference, and only then expose the mode as complete. "
        "Existing weapons, selection, and camera code are presentation/gameplay support, not the missing learning contract.",
    ),
    P("Tests that would buy confidence quickly", "H2Guide"),
    bullet("Settings cloning and track-keyed save paths."),
    bullet("Sensor widths and action-size configuration for every objective/AI pair."),
    bullet("Classic and NEAT CaptureState/RestoreState round trips."),
    bullet("Hot/cold tuning classification and manual-save protection."),
    bullet("Objective progression/terminal behavior, especially hoop crossing and inference progression."),
    PageBreak(),
]

# Page 13 questions and cram sheet
story += [
    label("Final revision"),
    P("12. Likely grader questions - short answers", "H1Guide"),
    data_table(
        [
            ["Question", "Answer to lead with"],
            ["What is the architectural center?", "SimulationManager is the composition root and tick router; it delegates lifecycle behavior to ITrainingParadigm."],
            ["Why a paradigm and an engine?", "Paradigm abstracts the whole training lifecycle. Engine is a lower, evolution-only abstraction for how a population is created, evolved, championed, and serialized."],
            ["How are algorithms compared fairly?", "They share objective-selected observations, the four flight actions, JetPhysics, spawn rules, reward, and terminal logic."],
            ["Why both JetAgent and JetMLAgent?", "JetAgent drives an in-process IBrain. JetMLAgent adapts the same jet/physics/objective to the ML-Agents communicator and external policy."],
            ["What happens every FixedUpdate?", "Manager routes to challenge, inference, or training. The active lifecycle advances; jets still integrate physics through Unity's fixed timestep."],
            ["What is saved?", "Settings, objective parameters, statistics/history, and engine-specific state. Evolution stores the population; RL snapshots trainer checkpoints."],
            ["What is the biggest incomplete feature?", "Dogfight training: gameplay pieces exist but objective and combat observation/reward contracts do not."],
            ["What pattern is used?", "Strategy for paradigms and engines, interfaces for objectives/sensors/brains, a composition root in SimulationManager, and a snapshot read model for UI."],
        ],
        [usable_w * 0.31, usable_w * 0.69],
        font_size=7.35,
    ),
    P("One-pass checklist", "H2Guide"),
    bullet("I can give the 60-second pitch without notes."),
    bullet("I can redraw Manager -> Paradigm -> Engine/Trainer -> Environment."),
    bullet("I can explain why the engine layer does not apply to PPO/SAC."),
    bullet("I can trace one Fixed NeuroEvo generation and one PPO/SAC episode."),
    bullet("I can explain BasicFlight (12), Waypoint (19), and the four flight outputs."),
    bullet("I can explain track-keyed saves, evolution state versus RL checkpoints, and hot versus cold changes."),
    bullet("I can name the honest limitations: incomplete Dogfight, external RL process, checkpoint-bounded save, trainer-based inference."),
    P("Tiny glossary", "H2Guide"),
    data_table(
        [
            ["Term", "Meaning"],
            ["Mode", "Objective family: Max Altitude, Flight School, or incomplete Dogfight."],
            ["Track", "Active scene identity used for settings, save slots, checkpoints, and RL run IDs."],
            ["Paradigm", "Top-level training lifecycle strategy: evolutionary or RL."],
            ["Engine", "Evolution-only population algorithm: Classic NeuroEvo or NEAT."],
            ["Brain", "Callable Unity-side policy; current PPO/SAC policies are not IBrain objects."],
            ["Champion", "All-time best evolutionary brain or best RL episode score metadata."],
            ["Inference", "One-policy replay with no learning and no new save."],
        ],
        [usable_w * 0.22, usable_w * 0.78],
        font_size=7.35,
    ),
    Spacer(1, 2 * mm),
    callout(
        "Last line to remember",
        "The project succeeds architecturally because the aircraft and task are shared, while the learning lifecycles remain correctly different.",
        accent=AMBER,
    ),
]


def build():
    OUT.parent.mkdir(parents=True, exist_ok=True)
    doc.build(story)
    print(OUT)


if __name__ == "__main__":
    build()

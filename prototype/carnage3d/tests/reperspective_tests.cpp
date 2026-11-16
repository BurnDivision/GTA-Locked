// Native unit tests for the engine-independent Reperspective camera/input modules.
// Build + run: prototype/carnage3d/tests/run_tests.sh <path to carnage3d/src>
#include "reperspective/CameraHeading.h"
#include "reperspective/TwinStick.h"

#include <cstdio>
#include <cmath>
#include <initializer_list>

using namespace reperspective;

static int gFailures = 0;
static int gChecks = 0;

#define CHECK(cond) do { ++gChecks; if (!(cond)) { ++gFailures; std::printf("FAIL %s:%d  %s\n", __FILE__, __LINE__, #cond); } } while (0)
#define CHECK_NEAR(a, b, eps) do { ++gChecks; double _a = (a), _b = (b); if (std::fabs(_a - _b) > (eps)) { ++gFailures; std::printf("FAIL %s:%d  %s = %g, expected %g (+-%g)\n", __FILE__, __LINE__, #a, _a, _b, (double)(eps)); } } while (0)

static float Deg(float d) { return d * kPi / 180.0f; }
static float ToDeg(float r) { return r * 180.0f / kPi; }

static void TestWrap()
{
    CHECK_NEAR(WrapPi(Deg(190)), Deg(-170), 1e-4);
    CHECK_NEAR(WrapPi(Deg(-190)), Deg(170), 1e-4);
    CHECK_NEAR(WrapPi(Deg(720 + 10)), Deg(10), 1e-4);
    CHECK_NEAR(AngleDelta(Deg(170), Deg(-170)), Deg(20), 1e-4);
    CHECK_NEAR(AngleDelta(Deg(-170), Deg(170)), Deg(-20), 1e-4);
    CHECK_NEAR(AngleDelta(Deg(359), Deg(1)), Deg(2), 1e-4);
}

static void TestSmoothAcrossWrap()
{
    // from 170 to -170 must go +20 degrees through 180, never back through 0
    float a = Deg(170);
    float maxStep = 0.0f;
    for (int i = 0; i < 120; ++i)
    {
        float prev = a;
        a = SmoothAngle(a, Deg(-170), 1.0f / 60.0f, SmoothingTimeConstant(Smoothing::Fast));
        maxStep = std::fmax(maxStep, std::fabs(AngleDelta(prev, a)));
        CHECK(std::fabs(AngleDelta(Deg(180), a)) <= Deg(10.01f)); // stays on the short arc
    }
    CHECK_NEAR(AngleDelta(a, Deg(-170)), 0.0, Deg(0.01f));
    CHECK(maxStep < Deg(10)); // no jump at the wrap (a wrap bug would step ~340 degrees)
}

static float SimulateStep(float dt, float seconds, Smoothing s)
{
    float a = 0.0f;
    int steps = (int) std::lround(seconds / dt);
    for (int i = 0; i < steps; ++i)
        a = SmoothAngle(a, Deg(90), dt, SmoothingTimeConstant(s));
    return a;
}

static void TestFrameRateIndependence()
{
    for (Smoothing s : { Smoothing::Fast, Smoothing::Smooth })
    {
        const float settle = SmoothingSettleTime(s);
        // frame times that divide the settle time exactly (40, 80 and 160 fps)
        const float a40 = SimulateStep(1.0f / 40.0f, settle, s);
        const float a80 = SimulateStep(1.0f / 80.0f, settle, s);
        const float a160 = SimulateStep(1.0f / 160.0f, settle, s);
        // 90% of the step is covered at the settle time, whatever the frame rate
        CHECK_NEAR(ToDeg(a40), 81.0, 0.1);
        CHECK_NEAR(ToDeg(a80), 81.0, 0.1);
        CHECK_NEAR(ToDeg(a160), 81.0, 0.1);
    }
    // instant
    CHECK_NEAR(SmoothAngle(0.0f, Deg(90), 1.0f / 60.0f, SmoothingTimeConstant(Smoothing::Instant)), Deg(90), 1e-5);
    // rate limit
    CHECK_NEAR(SmoothAngle(0.0f, Deg(90), 0.1f, 0.0f, Deg(100)), Deg(10), 1e-4);
    CHECK_NEAR(SmoothAngle(0.0f, Deg(-90), 0.1f, 0.0f, Deg(100)), Deg(-10), 1e-4);
}

static void TestTravelHeading()
{
    TravelHeadingTracker t;
    t.Reset(Deg(0));
    const float dt = 1.0f / 60.0f;

    // standing still or creeping: holds
    for (int i = 0; i < 120; ++i) t.Update(0.1f * std::cos(Deg(90)), 0.1f * std::sin(Deg(90)), dt);
    CHECK_NEAR(t.mHeading, 0.0, 1e-5);

    // rapid left/right alternation every 0.1 s at walking speed: holds
    for (int i = 0; i < 180; ++i)
    {
        float dir = ((i / 6) % 2) ? Deg(90) : Deg(-90);
        t.Update(2.0f * std::cos(dir), 2.0f * std::sin(dir), dt);
    }
    CHECK(std::fabs(t.mHeading) < Deg(1));

    // stop, then a sustained new direction: follows after the commit time
    for (int i = 0; i < 30; ++i) t.Update(0.0f, 0.0f, dt);
    for (int i = 0; i < 12; ++i) t.Update(2.0f * std::cos(Deg(90)), 2.0f * std::sin(Deg(90)), dt); // 0.2 s
    CHECK(std::fabs(t.mHeading) < Deg(1)); // not yet committed
    for (int i = 0; i < 60; ++i) t.Update(2.0f * std::cos(Deg(90)), 2.0f * std::sin(Deg(90)), dt);
    CHECK_NEAR(ToDeg(t.mHeading), 90.0, 2.0);

    // small adjustment (5 degrees): drifts slowly, no jump
    float before = t.mHeading;
    t.Update(2.0f * std::cos(Deg(95)), 2.0f * std::sin(Deg(95)), dt);
    CHECK(std::fabs(AngleDelta(before, t.mHeading)) < Deg(0.2f));

    // crossing 180 degrees
    t.Reset(Deg(170));
    for (int i = 0; i < 120; ++i) t.Update(2.0f * std::cos(Deg(-150)), 2.0f * std::sin(Deg(-150)), dt);
    CHECK_NEAR(ToDeg(AngleDelta(t.mHeading, Deg(-150))), 0.0, 2.0);
}

static void TestBasis()
{
    // classic north-up camera (heading -90 degrees): up = -Z, right = +X
    ScreenBasis b = BasisFromHeading(Deg(-90));
    CHECK_NEAR(b.mUp.x, 0.0, 1e-5); CHECK_NEAR(b.mUp.y, -1.0, 1e-5);
    CHECK_NEAR(b.mRight.x, 1.0, 1e-5); CHECK_NEAR(b.mRight.y, 0.0, 1e-5);

    Vec2 w = ScreenToWorld(b, MakeVec2(1.0f, 0.0f)); // stick right
    CHECK_NEAR(w.x, 1.0, 1e-5); CHECK_NEAR(w.y, 0.0, 1e-5);
    w = ScreenToWorld(b, MakeVec2(0.0f, -1.0f)); // stick up
    CHECK_NEAR(w.x, 0.0, 1e-5); CHECK_NEAR(w.y, -1.0, 1e-5);

    // rotated camera: screen-up along heading 30 degrees
    b = BasisFromHeading(Deg(30));
    w = ScreenToWorld(b, MakeVec2(0.0f, -1.0f));
    CHECK_NEAR(ToDeg(Heading(w)), 30.0, 1e-3);
    w = ScreenToWorld(b, MakeVec2(1.0f, 0.0f));
    CHECK_NEAR(ToDeg(Heading(w)), 120.0, 1e-3);
}

static void TestDeadZone()
{
    CHECK_NEAR(Length(ApplyDeadZone(MakeVec2(0.2f, 0.0f), 0.25f)), 0.0, 1e-6);
    CHECK_NEAR(Length(ApplyDeadZone(MakeVec2(1.0f, 0.0f), 0.25f)), 1.0, 1e-5);
    CHECK_NEAR(Length(ApplyDeadZone(MakeVec2(0.625f, 0.0f), 0.25f)), 0.5, 1e-5);
}

static void TestLatch()
{
    InputFrameLatch latch;
    ScreenBasis b0 = BasisFromHeading(Deg(0));
    ScreenBasis b1 = BasisFromHeading(Deg(60));
    Vec2 right = MakeVec2(1.0f, 0.0f);
    ScreenBasis used = latch.Update(right, b0);
    CHECK_NEAR(used.mUp.x, b0.mUp.x, 1e-6);
    used = latch.Update(right, b1); // camera rotated, stick still held: old mapping kept
    CHECK_NEAR(used.mUp.x, b0.mUp.x, 1e-6);
    used = latch.Update(MakeVec2(0.95f, 0.3f), b1); // small stick change (~17 deg): kept
    CHECK_NEAR(used.mUp.x, b0.mUp.x, 1e-6);
    used = latch.Update(MakeVec2(0.0f, -1.0f), b1); // new direction (90 deg): re-latched
    CHECK_NEAR(used.mUp.x, b1.mUp.x, 1e-6);
    latch.Update(MakeVec2(0.0f, 0.0f), b1); // released
    used = latch.Update(right, b0);
    CHECK_NEAR(used.mUp.x, b0.mUp.x, 1e-6);
}

static void TestEvaluate()
{
    TwinStickConfig cfg;
    ScreenBasis b = BasisFromHeading(Deg(-90)); // north-up
    Vec2 none;

    // strafe: move right while aiming up
    TwinStickOutput o = EvaluateTwinStick(MakeVec2(1, 0), b, MakeVec2(0, -1), b, false, none, false, cfg);
    CHECK(o.mMoving && o.mAiming && o.mHasFacing && o.mRun);
    CHECK_NEAR(ToDeg(Heading(o.mMoveDirection)), 0.0, 1e-3);    // +X
    CHECK_NEAR(ToDeg(Heading(o.mAimDirection)), -90.0, 1e-3);   // -Z (screen up)
    CHECK_NEAR(ToDeg(Heading(o.mFacingDirection)), -90.0, 1e-3); // facing follows aim
    CHECK(o.mFire); // full deflection fires (auto)

    // backwards: move down while aiming up, small aim deflection = no auto fire
    o = EvaluateTwinStick(MakeVec2(0, 0.5f), b, MakeVec2(0, -0.5f), b, false, none, false, cfg);
    CHECK(o.mMoving && !o.mRun && o.mAiming && !o.mFire);
    CHECK_NEAR(std::fabs(ToDeg(AngleDelta(Heading(o.mMoveDirection), Heading(o.mAimDirection)))), 180.0, 1e-3);

    // manual fire, auto fire off
    cfg.mAutoFire = false;
    o = EvaluateTwinStick(none, b, MakeVec2(1, 0), b, false, none, false, cfg);
    CHECK(o.mAiming && !o.mFire);
    o = EvaluateTwinStick(none, b, MakeVec2(1, 0), b, false, none, true, cfg);
    CHECK(o.mFire);
    cfg.mAutoFire = true;

    // no aim: facing follows movement
    o = EvaluateTwinStick(MakeVec2(-1, 0), b, none, b, false, none, false, cfg);
    CHECK(!o.mAiming && o.mHasFacing && !o.mFire);
    CHECK_NEAR(std::fabs(ToDeg(Heading(o.mFacingDirection))), 180.0, 1e-3);

    // stationary aiming
    o = EvaluateTwinStick(none, b, MakeVec2(0, 1), b, false, none, false, cfg);
    CHECK(!o.mMoving && o.mAiming);
    CHECK_NEAR(ToDeg(Heading(o.mFacingDirection)), 90.0, 1e-3);

    // mouse aim (world direction) never auto-fires
    o = EvaluateTwinStick(none, b, none, b, true, MakeVec2(0, 3), false, cfg);
    CHECK(o.mAiming && !o.mFire);
    CHECK_NEAR(ToDeg(Heading(o.mAimDirection)), 90.0, 1e-3);

    // inside the dead zones: nothing
    o = EvaluateTwinStick(MakeVec2(0.1f, 0.1f), b, MakeVec2(0.2f, 0), b, false, none, false, cfg);
    CHECK(!o.mMoving && !o.mAiming && !o.mHasFacing && !o.mFire);
}

static void TestCombatTurn()
{
    CHECK_NEAR(CombatTurnAim(0.0f, 1.0f, 0.2f, Deg(180), 0.5f), Deg(90), 1e-4); // right = clockwise on screen
    CHECK_NEAR(CombatTurnAim(0.0f, -1.0f, 0.2f, Deg(180), 0.5f), Deg(-90), 1e-4);
    CHECK_NEAR(CombatTurnAim(Deg(10), 0.1f, 0.2f, Deg(180), 0.5f), Deg(10), 1e-6);
}

// Closed loop: Reperspective camera following the persistent travel heading while the
// player holds the move stick to the right. With the latched input frame the world movement
// direction must stay constant (no circling); without it the player would spiral.
static float ClosedLoopTravelDrift(bool useLatch)
{
    const float dt = 1.0f / 60.0f;
    TravelHeadingTracker tracker;
    tracker.Reset(Deg(-90));
    float camera = Deg(-90);
    InputFrameLatch latch;
    TwinStickConfig cfg;
    float firstHeading = 0.0f, lastHeading = 0.0f, totalTurn = 0.0f;
    for (int i = 0; i < 60 * 6; ++i)
    {
        ScreenBasis current = BasisFromHeading(camera);
        ScreenBasis moveBasis = useLatch ? latch.Update(MakeVec2(1, 0), current) : current;
        TwinStickOutput o = EvaluateTwinStick(MakeVec2(1, 0), moveBasis, Vec2(), current, false, Vec2(), false, cfg);
        float h = Heading(o.mMoveDirection);
        if (i == 0) firstHeading = lastHeading = h;
        totalTurn += std::fabs(AngleDelta(lastHeading, h));
        lastHeading = h;
        tracker.Update(6.0f * o.mMoveDirection.x, 6.0f * o.mMoveDirection.y, dt);
        camera = SmoothAngle(camera, tracker.mHeading, dt, SmoothingTimeConstant(Smoothing::Fast));
    }
    // camera should end up aligned with the travel direction (player heading up the screen)
    if (useLatch)
    {
        CHECK_NEAR(ToDeg(AngleDelta(camera, lastHeading)), 0.0, 2.0);
    }
    (void) firstHeading;
    return ToDeg(totalTurn);
}

static void TestClosedLoop()
{
    const float withLatch = ClosedLoopTravelDrift(true);
    const float withoutLatch = ClosedLoopTravelDrift(false);
    std::printf("closed loop, stick held right 6 s: movement turned %.1f deg with latch, %.1f deg without\n", withLatch, withoutLatch);
    CHECK(withLatch < 1.0f);
    CHECK(withoutLatch > 180.0f); // demonstrates the feedback loop the latch prevents
}

int main()
{
    TestWrap();
    TestSmoothAcrossWrap();
    TestFrameRateIndependence();
    TestTravelHeading();
    TestBasis();
    TestDeadZone();
    TestLatch();
    TestEvaluate();
    TestCombatTurn();
    TestClosedLoop();
    std::printf("%d checks, %d failures\n", gChecks, gFailures);
    return gFailures ? 1 : 0;
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CampanhaRio.Kayak;
using CampanhaRio.River;
using UnityEngine;

namespace CampanhaRio.Dev
{
    /// <summary>
    /// DEBUG: measures the kayak feel with numbers, so two controllers can be compared (Stage 7 A/B).
    /// Scripted scenarios teleport the kayak and drive it through the normal input path (it is the IKayakInputSource),
    /// then one full autopilot run goes down River_01. Every value is sampled per physics step.
    /// Results: &lt;outputFolder&gt;/&lt;timestamp&gt;_&lt;label&gt;.csv + .md. Run it from CampanhaRio > Feel Benchmark (or batch).
    /// Random is seeded first so runs are repeatable.
    /// </summary>
    public class FeelBenchmark : MonoBehaviour, IKayakInputSource
    {
        public KayakController kayak;
        public string label = "baseline";
        public string outputFolder = "Assets/_Project/Docs/Benchmarks";
        [Tooltip("River_01 knots: a corridor stretch search range, and the gorge (start/end).")]
        public int corridorFromKnot = 2, corridorToKnot = 7, gorgeKnot = 7, gorgeEndKnot = 10;
        [Tooltip("Also run the autopilot down the whole river (the longest part).")]
        public bool autopilotRun = true;
        [Tooltip("Safety limit for the autopilot run (simulated seconds).")]
        public float autopilotMaxSeconds = 900f;

        public event Action OnFinished;
        public string CsvPath { get; private set; }

        struct Row { public string scenario, metric, unit, note; public float value; }
        readonly List<Row> rows = new List<Row>();

        RiverPath river;
        KayakAutopilot autopilot;
        float steer, rampedSteer; bool paddle, backPaddle, pressRoll;
        const float KeyboardSteerRamp = 12f;
        int bumps;
        readonly WaitForFixedUpdate step = new WaitForFixedUpdate();

        public KayakInputState ReadInput()
        {
            // Steer is pressed like a keyboard key: the same ramp as KayakInput.keyboardSteerResponse
            rampedSteer = Mathf.MoveTowards(rampedSteer, steer, KeyboardSteerRamp * Time.fixedDeltaTime);
            var state = new KayakInputState { steer = rampedSteer, paddle = paddle, backPaddle = backPaddle, roll = pressRoll };
            pressRoll = false;
            return state;
        }

        float Dt => Time.fixedDeltaTime;

        IEnumerator Start()
        {
            if (!kayak) kayak = KayakRegistry.Local ? KayakRegistry.Local : FindAnyObjectByType<KayakController>();
            river = kayak ? kayak.river : null;
            if (!river) river = RiverPath.Home;
            if (!kayak || !river) { Debug.LogError("[Campanha] FeelBenchmark: no kayak or river."); OnFinished?.Invoke(); yield break; }
            autopilot = kayak.GetComponent<KayakAutopilot>();
            kayak.OnImpact += (level, intensity, point, normal) => bumps++;

            yield return null; // let everything Start (drift spawns happen there)
            UnityEngine.Random.InitState(7);

            float started = Time.realtimeSinceStartup;
            yield return SpawnToCurrent();
            yield return LateralResponse();
            yield return YawInCurrent();
            yield return YawStillWater();
            yield return Settle();
            yield return BackwaterGlide();
            yield return BackwaterExit();
            yield return EddyParking();
            yield return EddyLineShear();
            yield return ImpactLevels();
            yield return Airtimes();
            yield return GorgeTopSpeed();
            if (autopilotRun && autopilot) yield return AutopilotRun();

            Release();
            Write(Time.realtimeSinceStartup - started);
            WriteTrace();
            OnFinished?.Invoke();
        }

        // ------------------------------------------------------------------ scenarios

        IEnumerator SpawnToCurrent()
        {
            float d = FindClearStretch(40f);
            var s = river.GetPointAtDistance(d);
            Place(s.point + s.right * s.thalweg, s.direction, 0f);
            float t = 0f, reached = -1f, current = 0f;
            while (t < 20f)
            {
                yield return step; t += Dt;
                current = kayak.CurrentSpeed;
                if (t > 0.1f && kayak.Speed >= 0.9f * current) { reached = t; break; }
            }
            Add("spawn_to_current", "time_to_90pct", reached, "s", $"from rest on the corridor thalweg at {d:0} m, current {current:0.00} m/s");
        }

        IEnumerator LateralResponse()
        {
            float d = FindClearStretch(40f);
            var s = river.GetPointAtDistance(d);
            Place(s.point + s.right * StartLateral(s), s.direction, 1f);
            yield return Hold(1f, 0f);
            float l0 = kayak.RiverSample.lateralOffset;
            var offsets = new List<float>();
            float peakSpeed = 0f;
            for (float t = 0f; t < StepWindow; t += Dt)
            {
                steer = 1f;
                yield return step;
                offsets.Add(kayak.RiverSample.lateralOffset - l0);
                peakSpeed = Mathf.Max(peakSpeed, Vector3.Dot(kayak.Velocity - kayak.CurrentVector, kayak.RiverSample.right));
            }
            steer = 0f;
            float max = offsets.Max();
            int i50 = offsets.FindIndex(o => o >= 0.5f * max);
            Add("lateral_step", "time_to_50pct_offset", (i50 + 1) * Dt, "s", $"full right steer for 2.5 s from the left side; max offset {max:0.00} m");
            Add("lateral_step", "offset_after_2_5s", max, "m", "");
            Add("lateral_step", "peak_lateral_speed", peakSpeed, "m/s", "relative to the water");
        }

        IEnumerator YawInCurrent()
        {
            float d = FindClearStretch(40f);
            var s = river.GetPointAtDistance(d);
            Place(s.point + s.right * StartLateral(s), s.direction, 1f);
            yield return Hold(1f, 0f);
            float peak = 0f, t90 = -1f;
            var angles = new List<float>();
            var yawRates = new List<float>();
            for (float t = 0f; t < StepWindow; t += Dt)
            {
                steer = 1f;
                yield return step;
                peak = Mathf.Max(peak, kayak.YawRate);
                angles.Add(AngleToFlow());
                yawRates.Add(kayak.YawRate);
            }
            float angle = angles.Max();
            for (int i = 0; i < angles.Count; i++) if (angles[i] >= 0.9f * angle) { t90 = (i + 1) * Dt; break; }
            float fullAngle = kayak.physics.maxSteerAngle;
            float visible = -1f, t80 = -1f;
            for (int i = 0; i < angles.Count; i++) if (angles[i] >= 0.8f * fullAngle) { t80 = (i + 1) * Dt; break; }
            for (int i = 0; i < yawRates.Count; i++) if (yawRates[i] >= visibleYawRate) { visible = (i + 1) * Dt; break; }
            Add("yaw_current", "input_to_visible_response", visible, "s", $"digital full steer (keyboard) until the yaw rate reaches {visibleYawRate:0} deg/s");
            Add("yaw_current", "time_to_80pct_steer", t80, "s", $"until the hull is at 80% of the full steer angle ({0.8f * fullAngle:0} deg to the flow)");
            float back = -1f;
            for (float t = 0f; t < 4f; t += Dt)
            {
                steer = 0f;
                yield return step;
                if (Mathf.Abs(AngleToFlow()) < 10f) { back = t + Dt; break; }
            }
            Add("yaw_current", "peak_yaw_rate", peak, "deg/s", "full right steer for 2.5 s in the corridor current");
            Add("yaw_current", "max_angle_to_flow", angle, "deg", "the held ferry angle");
            Add("yaw_current", "time_to_90pct_angle", t90, "s", "");
            Add("yaw_current", "return_to_downstream", back, "s", "after releasing, until within 10 deg of the flow");
        }

        IEnumerator YawStillWater()
        {
            var zone = Backwater();
            if (!zone) yield break;
            var s = river.Sample(zone.transform.position);
            Place(zone.transform.position, s.direction, 0f);
            yield return Hold(0.5f, 0f);
            float peak = 0f, sum = 0f; int n = 0;
            for (float t = 0f; t < 5f; t += Dt)
            {
                steer = 1f;
                yield return step;
                peak = Mathf.Max(peak, kayak.YawRate);
                if (t >= 2f) { sum += kayak.YawRate; n++; }
            }
            steer = 0f;
            Add("yaw_still", "peak_yaw_rate", peak, "deg/s", $"full right steer in the backwater (current {kayak.CurrentSpeed:0.00} m/s)");
            Add("yaw_still", "steady_yaw_rate", n > 0 ? sum / n : 0f, "deg/s", "mean over seconds 2-5");
        }

        /// <summary>Dropped onto still water: where does the hull float, and does it settle without jitter?</summary>
        IEnumerator Settle()
        {
            var zone = Backwater();
            if (!zone) yield break;
            var s = river.Sample(zone.transform.position);
            Vector3 p = zone.transform.position - s.right * Mathf.Min(7f, s.lateralOffset * 0.5f);
            Place(p, s.direction, 0f);
            float offset = 0f, vy2 = 0f, tilt = 0f; int n = 0;
            for (float t = 0f; t < 4f; t += Dt)
            {
                yield return step;
                if (t < 2f) continue;
                Vector3 k = kayak.transform.position;
                offset += k.y - river.GetWaterHeight(k); vy2 += kayak.Velocity.y * kayak.Velocity.y; n++;
                Vector3 e = kayak.transform.eulerAngles;
                tilt = Mathf.Max(tilt, Mathf.Abs(Mathf.DeltaAngle(0f, e.x)), Mathf.Abs(Mathf.DeltaAngle(0f, e.z)));
            }
            Add("settle", "waterline_offset", offset / Mathf.Max(n, 1), "m", "root height above the surface at rest (seconds 2-4); includes the gentle bob");
            Add("settle", "rms_vertical_speed", Mathf.Sqrt(vy2 / Mathf.Max(n, 1)), "m/s", "jitter check");
            Add("settle", "max_tilt", tilt, "deg", "largest pitch or roll at rest");
        }

        IEnumerator BackwaterGlide()
        {
            var zone = Backwater();
            if (!zone) yield break;
            var s = river.Sample(zone.transform.position);
            Vector3 heading = s.direction; // along the pocket, in its deeper half (the zone center is near the shallow shore)
            Vector3 start = zone.transform.position - s.right * Mathf.Min(7f, s.lateralOffset * 0.5f) - heading * 6f;
            Place(start, heading, 0f);
            paddle = true;
            yield return Hold(4f, 0f);
            paddle = false;
            float releaseSpeed = kayak.Speed;
            Vector3 from = kayak.transform.position;
            float stop = -1f;
            for (float t = 0f; t < 40f; t += Dt)
            {
                yield return step;
                if (Flat(kayak.Velocity - kayak.CurrentVector).magnitude < 0.1f) { stop = t + Dt; break; }
            }
            Add("backwater_glide", "glide_to_stop", stop, "s", $"after 4 s of paddling, released at {releaseSpeed:0.00} m/s, until < 0.1 m/s relative to the water");
            Add("backwater_glide", "glide_distance", Flat(kayak.transform.position - from).magnitude, "m", "");
            Add("backwater_glide", "release_speed", releaseSpeed, "m/s", $"start depth {Depth(start):0.0} m");
        }

        IEnumerator BackwaterExit()
        {
            var zone = Backwater();
            if (!zone) yield break;
            float restAlong = river.Sample(zone.transform.position).distanceAlong;
            Vector3 exit = river.GetPointAtDistance(restAlong + 30f).point;
            Place(zone.transform.position, Flat(exit - zone.transform.position), 0f);
            int strokes0 = kayak.StrokeCount;
            float left = -1f;
            for (float t = 0f; t < 40f; t += Dt)
            {
                steer = Mathf.Clamp(Vector3.SignedAngle(Flat(kayak.transform.forward), Flat(exit - kayak.transform.position), Vector3.up) * 0.025f, -1f, 1f);
                paddle = true;
                yield return step;
                Trace("backwater_exit");
                if (kayak.CurrentSpeed > 0.8f) { left = t + Dt; break; }
            }
            Release();
            Add("backwater_exit", "strokes_to_leave", kayak.StrokeCount - strokes0, "strokes", "paddle held from the backwater center toward the main current (> 0.8 m/s)");
            Add("backwater_exit", "time_to_leave", left, "s", "");
        }

        /// <summary>Paddle bow-first into the biggest rock at three speeds: which impact level, and how long until upright again?</summary>
        IEnumerator ImpactLevels()
        {
            // The biggest rock that can be reached in a straight line from upstream (nothing else in the way)
            FlowObstacle o = null; Vector3 start = Vector3.zero, dir = Vector3.forward; float glanceOffset = 0f;
            int mask = LayerMask.GetMask("Obstacle", "Environment");
            foreach (var candidate in FindObjectsByType<FlowObstacle>()
                .Where(x => river.Sample(x.transform.position).edgeDistance < -1f).OrderByDescending(x => x.Radius))
            {
                var cs = river.Sample(candidate.transform.position);
                var col = candidate.GetComponentInChildren<Collider>();
                Vector3 rock = col ? col.bounds.center : candidate.transform.position; rock.y = cs.point.y;
                float reach = col ? Mathf.Max(col.bounds.extents.x, col.bounds.extents.z) : candidate.Radius;
                Vector3 from = rock - cs.direction * (reach + 3f);
                Vector3 d = Flat(rock - from).normalized;
                if (!Physics.SphereCast(from + Vector3.up * 0.1f, 0.35f, d, out var hitInfo, reach + 4f, mask, QueryTriggerInteraction.Ignore)) continue;
                Transform h = hitInfo.collider.transform;
                if (!(h.IsChildOf(candidate.transform) || candidate.transform.IsChildOf(h))) continue;
                o = candidate; start = from; dir = d; glanceOffset = reach * 0.8f;
                break;
            }
            if (!o) yield break;
            // (speed, press Roll in the sweet spot, glancing along the flank instead of head-on)
            var runs = new[] { (1.2f, false, false), (3.5f, false, false), (5f, false, true), (7f, false, false), (7f, true, false) };
            foreach (var (speed, roll, glancing) in runs)
            {
                Vector3 flank = glancing ? new Vector3(dir.z, 0f, -dir.x) * (glanceOffset) : Vector3.zero; // hits the rock's side, sliding along it
                Place(start + flank, dir, 0f);
                kayak.Teleport(kayak.transform.position, kayak.transform.rotation, dir * speed);
                var body = kayak.GetComponent<Rigidbody>();
                ImpactLevel level = ImpactLevel.Bump; bool hit = false, capsized = false, recovered = false;
                float capsizeAt = 0f, uprightAt = -1f, t = 0f;
                float firstIntensity = 0f, firstNormalY = 0f;
                Action<ImpactLevel, float, Vector3, Vector3> onImpact = (l, i, p, n) => { if (!hit) { firstIntensity = i; firstNormalY = n.y; } if (!hit || l > level) level = l; hit = true; };
                Action onCapsize = () => { capsized = true; capsizeAt = t; };
                Action onRecovered = () => { recovered = true; uprightAt = t; };
                kayak.OnImpact += onImpact; kayak.OnCapsize += onCapsize; kayak.OnRecovered += onRecovered;
                bool pressed = false;
                for (; t < 8f && !(recovered || (hit && !capsized && t > 2f)); t += Dt)
                {
                    // Press Roll in the middle of the sweet spot, as a good player would
                    if (roll && !pressed && kayak.Simulation is KayakPhysics ph && ph.RollRing >= 0f
                        && ph.RollRing >= (ph.rollSweetSpot.x + ph.rollSweetSpot.y) * 0.5f) { pressRoll = true; pressed = true; }
                    Trace($"impact{speed:0.0}");
                    if (!hit && t < 6f) body.linearVelocity = new Vector3(dir.x * speed, body.linearVelocity.y, dir.z * speed); // hold the approach speed until contact
                    yield return step;
                }
                kayak.OnImpact -= onImpact; kayak.OnCapsize -= onCapsize; kayak.OnRecovered -= onRecovered;
                string name = $"impact_{speed:0.0}ms{(glancing ? "_glancing" : "")}{(roll ? "_roll" : "")}".Replace('.', '_');
                Add(name, "level", hit ? (int)level : -1, "level (0 bump 1 scrape 2 spin-out 3 capsize)", $"{(glancing ? "glancing along" : "bow-first into")} {o.name} at {speed:0.0} m/s{(roll ? ", Roll pressed in the sweet spot" : "")}; first contact intensity {firstIntensity:0.00}, normal y {firstNormalY:0.00}");
                Add(name, "capsize_to_upright", capsized ? (recovered ? uprightAt - capsizeAt : -1f) : 0f, "s", "0 = never capsized");
            }
            Release();
        }

        /// <summary>Drift past the biggest rock right along its eddy line, no input: how much does the shear spin the kayak?</summary>
        IEnumerator EddyLineShear()
        {
            var o = FindObjectsByType<FlowObstacle>()
                .Where(x => river.Sample(x.transform.position).edgeDistance < -1f)
                .OrderByDescending(x => x.Radius).FirstOrDefault();
            if (!o) yield break;
            var rock = river.Sample(o.transform.position);
            float side = rock.lateralOffset > 0f ? -1f : 1f; // pass on the open side
            var s = river.GetPointAtDistance(rock.distanceAlong - 10f);
            Place(s.point + s.right * (rock.lateralOffset + side * (o.Radius + 0.9f)), s.direction, 1f);
            float peak = 0f, shear = 0f;
            float startHeading = kayak.transform.eulerAngles.y;
            for (float t = 0f; t < 6f; t += Dt)
            {
                yield return step;
                peak = Mathf.Max(peak, Mathf.Abs(kayak.YawRate));
                shear = Mathf.Max(shear, Mathf.Abs(kayak.Shear));
            }
            float turned = Mathf.Abs(Mathf.DeltaAngle(startHeading, kayak.transform.eulerAngles.y));
            Add("eddy_line", "peak_yaw_rate", peak, "deg/s", $"drifting past {o.name} along its eddy line, no input for 6 s; peak bow-stern shear {shear:0.00} m/s");
            Add("eddy_line", "heading_change", turned, "deg", "");
        }

        IEnumerator EddyParking()
        {
            var eddies = FindObjectsByType<FlowObstacle>()
                .Where(o => river.Sample(o.transform.position).edgeDistance < -1f)
                .OrderByDescending(o => o.Radius).Take(2).ToList();
            for (int e = 0; e < eddies.Count; e++)
            {
                var o = eddies[e];
                var s = river.Sample(o.transform.position);
                // The deepest point of the pocket along its axis
                Vector3 best = o.transform.position; float bestW = 0f;
                for (float a = o.Radius; a < o.PocketLength; a += 0.25f)
                {
                    Vector3 p = o.transform.position + s.direction * a;
                    float w = o.Weight(p);
                    if (w > bestW) { bestW = w; best = p; }
                }
                Place(best, -s.direction, 0f); // parked facing upstream, as kayakers do
                float sum = 0f; int n = 0;
                for (float t = 0f; t < 8f; t += Dt)
                {
                    yield return step;
                    if (t >= 4f) { sum += kayak.Speed; n++; }
                }
                float weight = o.Weight(kayak.transform.position);
                string name = $"eddy_{e + 1}";
                Add(name, "holds", weight > 0.2f ? 1f : 0f, "bool", $"{o.name} (r {o.Radius:0.0} m, pocket {o.PocketLength:0.0} m) at {s.distanceAlong:0} m; no input for 8 s");
                Add(name, "mean_speed_4_8s", n > 0 ? sum / n : 0f, "m/s", "");
                Add(name, "drift_from_spot", Flat(kayak.transform.position - best).magnitude, "m", "");
            }
        }

        IEnumerator Airtimes()
        {
            var types = new[] { RiverFeatureType.Ledge, RiverFeatureType.ChuteWaveTrain, RiverFeatureType.PourOver, RiverFeatureType.LogRamp };
            var features = FindObjectsByType<RiverFeature>()
                .Where(f => types.Contains(f.type))
                .OrderBy(f => river.Sample(f.transform.position).distanceAlong).ToList();
            foreach (var f in features)
            {
                foreach (bool paddling in new[] { false, true })
                {
                    var fs = river.Sample(f.transform.position);
                    var s = river.GetPointAtDistance(fs.distanceAlong - 25f);
                    Place(s.point + s.right * fs.lateralOffset, s.direction, 1f);
                    float end = fs.distanceAlong + f.length + 12f;
                    string name = "air_" + Slug(f.name) + (paddling ? "_paddle" : "_drift");
                    float maxAir = 0f, total = 0f, takeoffSpeed = 0f, landing = 0f; int jumps = 0;
                    bool wasAir = false;
                    Action<float> onLand = air => landing = Mathf.Max(landing, kayak.LastLandingImpact);
                    kayak.OnLand += onLand;
                    for (float t = 0f; t < 30f && Along() < end; t += Dt)
                    {
                        var here = kayak.RiverSample;
                        float latSpeed = Vector3.Dot(kayak.Velocity, here.right);
                        steer = Mathf.Clamp((fs.lateralOffset - here.lateralOffset) * 0.35f - latSpeed * 0.3f, -1f, 1f);
                        paddle = paddling;
                        yield return step;
                        Trace(name);
                        if (kayak.IsAirborne && !wasAir) { jumps++; takeoffSpeed = Mathf.Max(takeoffSpeed, kayak.Speed); }
                        if (!kayak.IsAirborne && wasAir) { maxAir = Mathf.Max(maxAir, kayak.AirTime); total += kayak.AirTime; }
                        wasAir = kayak.IsAirborne;
                    }
                    kayak.OnLand -= onLand;
                    Release();

                    Add(name, "max_airtime", maxAir, "s", $"{f.type} at {fs.distanceAlong:0} m, lateral {fs.lateralOffset:0.0} m, {(paddling ? "paddle held" : "no paddling")}");
                    Add(name, "jumps", jumps, "count", "");
                    Add(name, "total_airtime", total, "s", "");
                    Add(name, "takeoff_speed", takeoffSpeed, "m/s", "");
                    Add(name, "landing_impact", landing, "m/s", "");
                }
            }
        }

        IEnumerator GorgeTopSpeed()
        {
            float from = river.GetKnotDistance(gorgeKnot), to = river.GetKnotDistance(gorgeEndKnot);
            var s = river.GetPointAtDistance(from);
            Place(s.point + s.right * s.thalweg, s.direction, 1f);
            float top = 0f, topCurrent = 0f, sum = 0f; int n = 0, bumps0 = bumps;
            float t = 0f;
            for (; t < 90f && Along() < to; t += Dt)
            {
                var here = kayak.RiverSample;
                float latSpeed = Vector3.Dot(kayak.Velocity, here.right);
                steer = Mathf.Clamp((here.thalweg - here.lateralOffset) * 0.35f - latSpeed * 0.3f, -1f, 1f);
                paddle = true;
                yield return step;
                top = Mathf.Max(top, kayak.Speed);
                topCurrent = Mathf.Max(topCurrent, kayak.CurrentSpeed);
                sum += kayak.Speed; n++;
            }
            Release();
            Add("gorge_paddle", "top_speed", top, "m/s", "paddle held on the thalweg, knots 7-10");
            Add("gorge_paddle", "mean_speed", n > 0 ? sum / n : 0f, "m/s", "");
            Add("gorge_paddle", "top_current", topCurrent, "m/s", "");
            Add("gorge_paddle", "section_time", t, "s", "");
            Add("gorge_paddle", "bumps", bumps - bumps0, "count", "");
        }

        IEnumerator AutopilotRun()
        {
            Release();
            kayak.Respawn();
            autopilot.SetActive(true);
            float gorgeFrom = river.GetKnotDistance(gorgeKnot), gorgeTo = river.GetKnotDistance(gorgeEndKnot);
            float stopAt = river.Length - kayak.riverEndMargin - 2f;
            float t = 0f, gorgeTop = 0f, top = 0f, maxAir = 0f;
            int bumps0 = bumps, jumps = 0, eddyCatches = 0, eddyHolds = 0;
            var airByFeature = new Dictionary<string, float>();
            string takeoffFeature = "-";
            bool wasAir = false;
            var lastState = autopilot.State;
            float eddySpeedSum = 0f; int eddySpeedN = 0;
            int leaveStrokes0 = 0; float leaveStart = 0f; int leaveStrokes = -1; float leaveTime = -1f;
            for (; t < autopilotMaxSeconds && Along() < stopAt; t += Dt)
            {
                yield return step;
                float along = Along();
                Trace("autopilot_" + autopilot.State);
                top = Mathf.Max(top, kayak.Speed);
                if (along > gorgeFrom && along < gorgeTo) gorgeTop = Mathf.Max(gorgeTop, kayak.Speed);
                if (kayak.IsAirborne && !wasAir) { jumps++; takeoffFeature = kayak.FeatureName; }
                if (!kayak.IsAirborne && wasAir)
                {
                    maxAir = Mathf.Max(maxAir, kayak.AirTime);
                    airByFeature.TryGetValue(takeoffFeature, out float a);
                    airByFeature[takeoffFeature] = Mathf.Max(a, kayak.AirTime);
                }
                wasAir = kayak.IsAirborne;

                var state = autopilot.State;
                if (state != lastState)
                {
                    if (state == AutopilotState.InEddy) eddyCatches++;
                    if (lastState == AutopilotState.InEddy && eddySpeedN > 0 && eddySpeedSum / eddySpeedN < 0.6f) eddyHolds++;
                    if (state == AutopilotState.InEddy) { eddySpeedSum = 0f; eddySpeedN = 0; }
                    if (state == AutopilotState.Leaving) { leaveStrokes0 = kayak.StrokeCount; leaveStart = t; }
                    if (lastState == AutopilotState.Leaving) { leaveStrokes = kayak.StrokeCount - leaveStrokes0; leaveTime = t - leaveStart; }
                    lastState = state;
                }
                if (state == AutopilotState.InEddy) { eddySpeedSum += kayak.Speed; eddySpeedN++; }
            }
            autopilot.SetActive(false);
            Add("autopilot_run", "run_time", t, "s", $"spawn to {stopAt:0} m (includes the rest stop and eddy stops)");
            Add("autopilot_run", "top_speed", top, "m/s", "");
            Add("autopilot_run", "gorge_top_speed", gorgeTop, "m/s", "");
            Add("autopilot_run", "bumps", bumps - bumps0, "count", "");
            Add("autopilot_run", "jumps", jumps, "count", "");
            Add("autopilot_run", "max_airtime", maxAir, "s", "");
            foreach (var kv in airByFeature.OrderByDescending(k => k.Value))
                Add("autopilot_run", "airtime_" + Slug(kv.Key), kv.Value, "s", "longest jump taking off there");
            Add("autopilot_run", "eddy_catches", eddyCatches, "count", "");
            Add("autopilot_run", "eddy_holds", eddyHolds, "count", "mean speed < 0.6 m/s while parked");
            Add("autopilot_run", "backwater_exit_strokes", leaveStrokes, "strokes", "autopilot Leaving state");
            Add("autopilot_run", "backwater_exit_time", leaveTime, "s", "");
        }


        // ------------------------------------------------------------------ optional per-step trace (CR_BENCH_TRACE = file path)

        StringBuilder trace;
        float traceTime;

        void Trace(string scenario)
        {
            if (trace == null)
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CR_BENCH_TRACE"))) return;
                trace = new StringBuilder("scenario,t,along,lateral,y,surface,vy,speed,current,wet,airborne,air_time,pitch,roll,yaw_rate,strokes,bumps,heading,course,mode\n");
            }
            traceTime += Dt;
            var s = kayak.RiverSample;
            Vector3 p = kayak.transform.position;
            Vector3 e = kayak.transform.eulerAngles;
            var inv = CultureInfo.InvariantCulture;
            int wet = kayak.Simulation is KayakPhysics ph ? ph.WetPoints : -1;
            trace.Append(scenario).Append(',').Append(string.Join(",", new[] { traceTime, s.distanceAlong, s.lateralOffset, p.y, river.GetWaterHeight(p), kayak.Velocity.y,
                kayak.Speed, kayak.CurrentSpeed, wet, kayak.IsAirborne ? 1 : 0, kayak.AirTime, Mathf.DeltaAngle(0f, e.x), Mathf.DeltaAngle(0f, e.z), kayak.YawRate,
                kayak.StrokeCount, bumps, e.y, Mathf.Atan2(kayak.Velocity.x, kayak.Velocity.z) * Mathf.Rad2Deg }.Select(v => v.ToString("0.###", inv)))).Append(',').Append(kayak.Mode).Append('\n');
        }

        void WriteTrace()
        {
            string path = Environment.GetEnvironmentVariable("CR_BENCH_TRACE");
            if (trace != null && !string.IsNullOrEmpty(path)) File.WriteAllText(path, trace.ToString());
        }
        // ------------------------------------------------------------------ helpers

        const float StepWindow = 2.5f;
        /// <summary>Yaw rate that reads as "the kayak is turning" (deg/s).</summary>
        const float visibleYawRate = 8f;

        /// <summary>Start on the left side so a full right steer has room before the bank.</summary>
        static float StartLateral(RiverPath.RiverSample s) => -Mathf.Max(0f, s.leftWidth - 2.5f);

        int alongHint = -1;
        /// <summary>Distance along the river at the kayak's actual position (RiverSample lags one step after a teleport).</summary>
        float Along() => river.Sample(kayak.transform.position, ref alongHint).distanceAlong;

        IEnumerator Hold(float seconds, float steerValue)
        {
            for (float t = 0f; t < seconds; t += Dt) { steer = steerValue; yield return step; }
        }

        /// <summary>Teleport, heading along dir, moving with <paramref name="currentShare"/> of the local current.</summary>
        void Place(Vector3 p, Vector3 dir, float currentShare)
        {
            Release();
            kayak.InputSource = this;
            p.y = river.GetWaterHeight(p);
            Vector3 v = river.Sample(p).waterVelocity * currentShare;
            kayak.Teleport(p, Quaternion.LookRotation(Flat(dir).normalized, Vector3.up), v);
        }

        void Release() { steer = rampedSteer = 0f; paddle = backPaddle = false; }

        float AngleToFlow()
        {
            Vector3 flow = Flat(kayak.CurrentVector);
            if (flow.sqrMagnitude < 1e-4f) flow = kayak.RiverSample.direction;
            return Vector3.SignedAngle(flow, Flat(kayak.transform.forward), Vector3.up);
        }

        /// <summary>First stretch of the corridor with no rocks, features or zones over the given length.</summary>
        float FindClearStretch(float length)
        {
            float from = river.GetKnotDistance(corridorFromKnot) + 10f, to = river.GetKnotDistance(corridorToKnot) - length;
            int mask = LayerMask.GetMask("Obstacle");
            for (float d = from; d < to; d += 5f)
            {
                bool clear = true;
                for (float x = 0f; x <= length && clear; x += 4f)
                {
                    var s = river.GetPointAtDistance(d + x);
                    if (Physics.CheckSphere(s.point, Mathf.Min(s.leftWidth, s.rightWidth), mask, QueryTriggerInteraction.Ignore)) clear = false;
                    else if (river.FeatureAt(s.point, out float w) != null && w > 0.01f) clear = false;
                    else if (s.zone != null) clear = false;
                }
                if (clear) return d;
            }
            return from;
        }

        float Depth(Vector3 p) => Physics.Raycast(new Vector3(p.x, river.GetWaterHeight(p) + 5f, p.z), Vector3.down, out var hit, 30f, LayerMask.GetMask("Environment")) ? river.GetWaterHeight(p) - hit.point.y : -1f;

        static CurrentZone Backwater()
        {
            foreach (var zone in FindObjectsByType<CurrentZone>())
                if (zone.type == CurrentZoneType.Backwater) return zone;
            return null;
        }

        void Add(string scenario, string metric, float value, string unit, string note) =>
            rows.Add(new Row { scenario = scenario, metric = metric, value = value, unit = unit, note = note });

        static string Slug(string s)
        {
            if (s == "-") return "no_feature";
            var sb = new StringBuilder();
            foreach (char c in s.ToLowerInvariant()) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString().Trim('_').Replace("__", "_");
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        void Write(float realSeconds)
        {
            Directory.CreateDirectory(outputFolder);
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
            CsvPath = Path.Combine(outputFolder, $"{stamp}_{label}.csv").Replace('\\', '/');
            var inv = CultureInfo.InvariantCulture;
            var csv = new StringBuilder("scenario,metric,value,unit,note\n");
            foreach (var r in rows)
                csv.Append(r.scenario).Append(',').Append(r.metric).Append(',').Append(r.value.ToString("0.###", inv)).Append(',')
                   .Append(r.unit).Append(",\"").Append(r.note.Replace("\"", "'")).Append("\"\n");
            File.WriteAllText(CsvPath, csv.ToString());

            var md = new StringBuilder();
            md.Append($"# Feel benchmark: {label}\n\n");
            md.Append($"{DateTime.Now:yyyy-MM-dd HH:mm}, River_01, controller `{ControllerName()}`, fixed step {Time.fixedDeltaTime:0.###} s, ");
            md.Append($"{realSeconds:0} s real time. Values are sampled every physics step. Random seeded for repeatability.\n\n");
            md.Append("| Scenario | Metric | Value | Unit | Note |\n|---|---|---|---|---|\n");
            foreach (var r in rows) md.Append($"| {r.scenario} | {r.metric} | {r.value.ToString("0.###", inv)} | {r.unit} | {r.note} |\n");
            md.Append("\n-1 means the event never happened within the time limit.\n");
            File.WriteAllText(Path.ChangeExtension(CsvPath, ".md"), md.ToString());
            Debug.Log($"[Campanha] Feel benchmark written: {CsvPath} ({rows.Count} values)");
        }

        string ControllerName() => kayak.Simulation != null ? kayak.Simulation.GetType().Name : "KayakPhysics";
    }
}

using System;
using System.Collections.Generic;

namespace CampusCommons.Webcam
{
    public struct StudyInterval
    {
        public double Start, End;
        public string State;
    }

    public static class StudyTime
    {
        public const long DayMilliseconds = 86400000;
        const long KoreaOffsetMilliseconds = 32400000;

        public static long Overlap(long start, long end, long from, long to) =>
            Math.Max(0, Math.Min(end, to) - Math.Max(start, from));

        public static long DayStart(long utcMs) =>
            ((utcMs + KoreaOffsetMilliseconds) / DayMilliseconds) * DayMilliseconds - KoreaOffsetMilliseconds;

        public static string Clock(long ms)
        {
            long seconds = Math.Max(0, ms) / 1000;
            return (seconds / 3600).ToString("00") + ":" +
                (seconds / 60 % 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        }
    }

    public sealed class StudyTracker
    {
        const double EvidenceWindow = 3;
        const double RecentWeightSeconds = .8;
        const double StudyConfirmation = 3;
        const double NonstudyConfirmation = 2;
        const double DrowsyConfirmation = 8;
        const string Studying = "studying";
        const string Other = "other";

        static readonly string[] States = { Studying, "phone", Other, "away", "drowsy" };

        sealed class Sample
        {
            public double CapturedAt;
            public double[] Scores;
        }

        readonly List<Sample> samples = new List<Sample>();
        readonly List<StudyInterval> intervals = new List<StudyInterval>();
        double lastCapture = double.NaN;
        double committedUntil, timeCandidateSince, labelCandidateSince;
        bool studying, timeTransitionPending;
        string labelCandidate;

        public double StudySeconds { get; private set; }
        public string Label { get; private set; }

        public void Observe(double capturedAt, double[] scores)
        {
            if (double.IsNaN(capturedAt) || double.IsInfinity(capturedAt) || !ValidScores(scores)) return;
            if (capturedAt <= lastCapture) return;
            if (capturedAt - lastCapture > EvidenceWindow) Break();
            if (double.IsNaN(lastCapture)) committedUntil = capturedAt;
            lastCapture = capturedAt;

            samples.RemoveAll(sample => capturedAt - sample.CapturedAt > EvidenceWindow);
            samples.Add(new Sample { CapturedAt = capturedAt, Scores = (double[])scores.Clone() });
            var weightedScores = new double[States.Length];
            double totalWeight = 0;
            foreach (var sample in samples)
            {
                double weight = Math.Exp((sample.CapturedAt - capturedAt) / RecentWeightSeconds);
                totalWeight += weight;
                for (int i = 0; i < States.Length; i++) weightedScores[i] += weight * sample.Scores[i];
            }

            int best = 0;
            for (int i = 1; i < States.Length; i++)
                if (weightedScores[i] > weightedScores[best]) best = i;

            // Group nonstudy evidence so alternating activities cannot keep a study interval open.
            bool studyEvidence = weightedScores[0] > totalWeight * .5;
            string candidate = studyEvidence ? Studying : best == 0 ? Other : States[best];
            if (candidate != labelCandidate)
            {
                labelCandidate = candidate;
                labelCandidateSince = capturedAt;
            }
            double labelDelay = candidate == "drowsy" ? DrowsyConfirmation :
                candidate == Studying ? StudyConfirmation : NonstudyConfirmation;
            if (capturedAt - labelCandidateSince >= labelDelay) Label = candidate;

            if (studyEvidence == studying)
            {
                timeTransitionPending = false;
                Commit(capturedAt, RecordedState());
            }
            else
            {
                if (!timeTransitionPending)
                {
                    timeTransitionPending = true;
                    timeCandidateSince = capturedAt;
                }
                double timeDelay = studyEvidence ? StudyConfirmation : NonstudyConfirmation;
                if (capturedAt - timeCandidateSince >= timeDelay)
                {
                    Commit(timeCandidateSince, studying ? Studying : Other);
                    studying = studyEvidence;
                    timeTransitionPending = false;
                    Commit(capturedAt, RecordedState());
                }
            }

            if (!studying && !timeTransitionPending && Label == Studying) Label = Other;
        }

        string RecordedState()
        {
            if (studying) return Studying;
            return Label == null || Label == Studying ? Other : Label;
        }

        static bool ValidScores(double[] scores)
        {
            if (scores == null || scores.Length != States.Length) return false;
            double sum = 0;
            foreach (double score in scores)
            {
                if (double.IsNaN(score) || double.IsInfinity(score) || score < 0 || score > 1) return false;
                sum += score;
            }
            return Math.Abs(sum - 1) <= .001;
        }

        void Commit(double end, string state)
        {
            if (end <= committedUntil) return;
            if (state == Studying) StudySeconds += end - committedUntil;
            intervals.Add(new StudyInterval { Start = committedUntil, End = end, State = state });
            committedUntil = end;
        }

        internal void DrainIntervals(Action<StudyInterval> consume)
        {
            foreach (var interval in intervals) consume(interval);
            intervals.Clear();
        }

        public void Break()
        {
            // An unresolved transition or a gap provides no additional observed time.
            samples.Clear();
            studying = timeTransitionPending = false;
            lastCapture = double.NaN;
            Label = labelCandidate = null;
        }
    }
}

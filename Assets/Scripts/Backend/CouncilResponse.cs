using System;

namespace UnderstudyKingdom.Backend
{
    // Bundles the request and response DTOs for the council endpoints in one
    // file, matching DuelRequest.cs's own precedent of grouping a feature's
    // small wire-shape types together rather than one file per type.
    [Serializable]
    public class CreateCouncilRequest
    {
        public string name;
    }

    [Serializable]
    public class JoinCouncilRequest
    {
        public string joinCode;
    }

    // Shared response shape for POST /api/v1/councils, POST
    // /api/v1/councils/join, and GET /api/v1/councils/me -- all three
    // server endpoints return this exact shape. See
    // docs/superpowers/specs/2026-09-03-council-social-design.md.
    [Serializable]
    public class CouncilResponse
    {
        public string id;
        public string name;
        public string joinCode;
        public int memberCount;
        public int totalDecisions;
        public int milestoneThreshold;
        public bool milestoneReached;
        public bool rewardEligible;
    }
}

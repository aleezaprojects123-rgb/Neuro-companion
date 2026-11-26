using UnityEngine;

namespace NeuroCompanion.Environment.Birds
{
    public enum BirdSpecies
    {
        HouseSparrow,
        RedVentedBulbul,
        EurasianCollaredDove,
        CommonMyna,
        JungleBabbler
    }

    public enum BirdState
    {
        IdleResting,
        Perched,
        Walking,
        TakingOff,
        Flying,
        Landing
    }

    [System.Serializable]
    public class BirdSpeciesProfile
    {
        public BirdSpecies species;
        public string commonName;
        public string scientificName;
        public float scale = 1.0f;
        public float bodyLength = 0.15f;
        public float bodyWidth = 0.07f;
        public float wingLength = 0.14f;
        public float tailLength = 0.08f;
        public float beakLength = 0.025f;
        public float flapFrequency = 7.0f;
        public float flightSpeed = 2.4f;
        public bool hasCrest = false;
        public bool hasRedVent = false;
        public bool hasNeckCollar = false;
        public bool hasYellowEyePatch = false;
        public bool canWalkOnGround = false;
    }
}

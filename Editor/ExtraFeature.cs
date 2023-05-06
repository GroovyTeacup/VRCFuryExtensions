using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VF.Component;
using VF.Model.Feature;
using Action = VF.Model.StateAction.Action;

namespace VF.Model.Feature
{
    [Serializable]
    public class LightingControl : NewFeatureModel
    {
        public SkinnedMeshRenderer[] meshExclusions;
        public string[] materialExclusions;
    }

    [Serializable]
    public class MeshSimplify : NewFeatureModel
    {
        public SkinnedMeshRenderer singleRenderer;
        public float quality = 0.5f;
        public List<SkinnedMeshRenderer> testThing;
    }
}
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VRC.SDKBase;
using UnityMeshSimplifier;

namespace VF.Feature {
    public class MeshSimplifyBuilder : FeatureBuilder<MeshSimplify> {
        public override string GetEditorTitle() {
            return "Mesh Simplifier";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will automatically decimate a provided mesh at runtime, reducing polycount without affecting the original mesh."
            ));
            
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("singleRenderer"), "Mesh To Optimize"));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("quality"), "Decimation Quality"));
            
            return content;
        }

        public override bool AvailableOnProps() {
            return true;
        }

        [FeatureBuilderAction]
        public void Apply() {

            SkinnedMeshRenderer renderer = model.singleRenderer;

            if (renderer == null) return;

            Mesh sourceMesh = renderer.sharedMesh;
            if (sourceMesh == null)
                return;

            var meshSimplifier = new MeshSimplifier();
            meshSimplifier.Initialize(sourceMesh);

            meshSimplifier.SimplifyMesh(model.quality);

            // Create our final mesh, save the asset, and apply it to our skinned mesh
            var simplifiedMesh = meshSimplifier.ToMesh();
            simplifiedMesh.name = sourceMesh.name + "_VRCF_Simplified";

            var meshCopy = mutableManager.MakeMutable(simplifiedMesh);
            VRCFuryEditorUtils.MarkDirty(meshCopy);
            renderer.sharedMesh = meshCopy;
            VRCFuryEditorUtils.MarkDirty(renderer);
        }
    }
}

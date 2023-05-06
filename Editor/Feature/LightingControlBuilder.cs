using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Thry;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using Object = UnityEngine.Object;

namespace VF.Feature
{
    public class MaterialPropertyGroup
    {
        public List<MaterialPropertyInfo> PropertyInfos { get; set; }
        public BlendTree BlendTree { get; set; }
        public string GroupName { get; set; }
        public VFAFloat Parameter { get; set; }
        public string ParameterName { get; set; }
        public float ParameterDefault { get; set; }
        public AnimationClip ClipStart;
        public AnimationClip ClipEnd;
        public string MenuLabel { get; set; }

        public void AddPropertyInfo(string propertyName, float startValue, float endValue, bool poiProperty = false)
        {
            PropertyInfos.Add(new MaterialPropertyInfo(propertyName, startValue, endValue, poiProperty));
        }

        public void InitializeAnimations(ControllerManager fx)
        {
            Parameter = fx.NewFloat(ParameterName, true, ParameterDefault, true);
            ClipStart = fx.NewClip(GroupName + "_Start");
            ClipEnd = fx.NewClip(GroupName + "_End");

            BlendTree = fx.NewBlendTree(GroupName + "_BT");
            BlendTree.blendParameter = Parameter.Name();
            BlendTree.blendType = BlendTreeType.Simple1D;
            BlendTree.minThreshold = 0;
            BlendTree.maxThreshold = 1;
            BlendTree.useAutomaticThresholds = true;
            BlendTree.children = new[]
            {
                new ChildMotion {motion = ClipStart, timeScale = 1, threshold = 0},
                new ChildMotion {motion = ClipEnd, timeScale = 1, threshold = 1}
            };
        }

        public void CreateLayerAndMenuOption(ControllerManager fx, MenuManager menu)
        {
            var layer = fx.NewLayer(GroupName);
            var state = layer.NewState(GroupName + " Blend Tree").WithAnimation(BlendTree);

            menu.NewMenuSlider("Lighting/" + MenuLabel, Parameter);
        }

        public MaterialPropertyGroup(string groupName, string parameterName, float defaultParameterValue, string menuName = null)
        {
            GroupName = groupName;
            ParameterName = parameterName;
            ParameterDefault = defaultParameterValue;
            MenuLabel = menuName ?? groupName;
            PropertyInfos = new List<MaterialPropertyInfo>();
        }
    }

    public class MaterialPropertyInfo
    {
        public string PropertyName { get; set; }
        public float StartValue { get; set; }
        public float EndValue { get; set; }
        public bool PoiProperty { get; set; }

        public MaterialPropertyInfo(string propertyName, float startValue, float endValue, bool poiProperty = false)
        {
            PropertyName = propertyName;
            StartValue = startValue;
            EndValue = endValue;
            PoiProperty = poiProperty;
        }
    }


    public class LightingControlBuilder : FeatureBuilder<LightingControl>
    {
        public override string GetEditorTitle()
        {
            return "Lighting Controls";
        }

        public override VisualElement CreateEditor(SerializedProperty prop)
        {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "Automatically creates menu-based lighting controls for your materials."
            ));

            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("meshExclusions"), ""));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("materialExclusions"), ""));

            return content;
        }

        public override bool AvailableOnProps()
        {
            return false;
        }

        [FeatureBuilderAction]
        public void Apply()
        {
            var fx = GetFx();

            var lightingControlGroup = new MaterialPropertyGroup("Lighting Add", "lightAdd", 0);
            lightingControlGroup.AddPropertyInfo("_PPLightingAddition", 0, 1, true); // Poiyomi
            lightingControlGroup.AddPropertyInfo("Unlit_Intensity", 0, 4); // UCTS
            lightingControlGroup.AddPropertyInfo("_AsUnlit", 0, 1); // lilToon
            lightingControlGroup.InitializeAnimations(fx);

            var lightingMultControlGroup = new MaterialPropertyGroup("Lighting Multiplier", "lightMult", 1, "Lighting Mult");
            lightingMultControlGroup.AddPropertyInfo("_PPLightingMultiplier", 0, 1, true); // Poiyomi
            lightingMultControlGroup.InitializeAnimations(fx);

            var greyscaleControlGroup = new MaterialPropertyGroup("Lighting Greyscale", "lightGreyscale", 0);
            greyscaleControlGroup.AddPropertyInfo("_LightingMonochromatic", 0, 1, true); // Poiyomi
            greyscaleControlGroup.AddPropertyInfo("_LightingAdditiveMonochromatic", 0, 1, true); // Poiyomi
            greyscaleControlGroup.AddPropertyInfo("_MonochromeLighting", 0, 1); // lilToon
            greyscaleControlGroup.InitializeAnimations(fx);

            var emissionStrengthControlGroup = new MaterialPropertyGroup("Emission Strength", "emissionStrength", 0.25f, "Emission Mult");
            emissionStrengthControlGroup.AddPropertyInfo("_PPEmissionMultiplier", 0, 4, true); // Poiyomi
            emissionStrengthControlGroup.InitializeAnimations(fx);

            var propertyGroups = new List<MaterialPropertyGroup> { lightingControlGroup, greyscaleControlGroup, lightingMultControlGroup, emissionStrengthControlGroup };

            var skins = avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            foreach (SkinnedMeshRenderer skin in skins)
            {
                if (model.meshExclusions.Contains(skin)) { continue; };

                foreach (var propertyGroup in propertyGroups)
                {
                    foreach (var materialPropertyInfo in propertyGroup.PropertyInfos)
                    {
                        var property = MaterialEditor.GetMaterialProperty(skin.sharedMaterials, materialPropertyInfo.PropertyName);
                        if (property != null)
                        {
                            MaterialProperty(propertyGroup.ClipStart, skin, materialPropertyInfo.PropertyName, materialPropertyInfo.StartValue);
                            MaterialProperty(propertyGroup.ClipEnd, skin, materialPropertyInfo.PropertyName, materialPropertyInfo.EndValue);

                            if (!materialPropertyInfo.PoiProperty) continue;

                            foreach (var mat in skin.sharedMaterials)
                            {
                                if (model.materialExclusions.Contains(mat.name)) continue;

                                SetPoiMaterialTagAnimated(mat, property);
                            }
                        }
                    }
                }
            }

            foreach (var group in propertyGroups)
            {
                group.CreateLayerAndMenuOption(fx, manager.GetMenu());
            }
        }

        public void MaterialProperty(AnimationClip clip, SkinnedMeshRenderer skin, string property, float value)
        {
            clip.SetCurve(clipBuilder.GetPath(skin.gameObject), typeof(SkinnedMeshRenderer), "material." + property, AnimationCurve.Constant(0, 0, value));
        }

        private bool SetPoiMaterialTagAnimated(Material mat, MaterialProperty property)
        {
            if (ShaderOptimizer.IsAnimated(mat, property.name))
            {
                return true;
            }

            if (ShaderOptimizer.IsMaterialLocked(mat))
            {
                Debug.Log("Unlocking poi material " + mat.name);
                ShaderOptimizer.SetLockedForAllMaterials(new[] { mat }, 0, true, false, true);
            }

            mat.SetOverrideTag(property.name + ShaderOptimizer.AnimatedTagSuffix, "1");
            VRCFuryEditorUtils.MarkDirty(mat);

            return true;
        }
    }
}

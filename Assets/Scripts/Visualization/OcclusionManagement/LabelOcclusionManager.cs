﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Scenery.RoadNetwork;
using UnityEditor;
using UnityEngine;
using Utils;
using Utils.AdditionalMath;

namespace Visualization.Labels {
    public class LabelOcclusionManager : MonoBehaviour {
        public RectTransform labelObject;

        /// <summary>
        /// All Labels that are managed by this OcclusionManager
        /// </summary>
        public List<ScreenLabel> AllLabels { get; set; } = new List<ScreenLabel>();
        
        // information about the screen
        private const float MaxHeight = 1080f;
        private const float MaxWidth = 1920f;
        
        // padding between labels
        private const float Padding = 40f;

        // information about the labels to be placed
        private int _labelHeight = 270;
        private int _labelWidth = 400;

        // radii of the circles for detecting and placing the labels
        private int _detectionRadius;
        
        // values for placing labels (when they would be out of bounds)
        private float _lowestY;

        // maximum Amount of Labels per side
        private int _maxLabelsPerSide;
        
        // main camera
        private ExtendedCamera _mainCamera;

        private ScreenLabel[] _activeRight = new ScreenLabel[0];
        private ScreenLabel[] _activeLeft = new ScreenLabel[0];

        private void Start() {
            _mainCamera = FindObjectOfType<ExtendedCamera>();
            
            RecalculateParameters();
        }

        private void RecalculateParameters() {
            var labelScale = labelObject.localScale.x;
            var rect = labelObject.rect;
            
            _labelHeight = (int) (rect.height * labelScale);
            _labelWidth = (int) (rect.width * labelScale);
            
            _detectionRadius = 500;

            _maxLabelsPerSide = Convert.ToInt32(Math.Floor(MaxHeight / _labelHeight));
            
            _lowestY = -MaxHeight / 2f + _labelHeight / 2f + Padding;
        }

        private void LateUpdate() {
            if (Time.frameCount % 5 != 0) return; // reduce the load by only updating every 5 frames
            
            PickActiveLabels();
        }

        private void OnGUI() {
            //return; // TODO maybe reenable
            
            GUI.depth = -100;
            
            foreach (var label in _activeRight) {
                Drawing.DrawLine(LocalScreenToActualScreenPoint(label.AnchorScreenPosition),
                    LocalScreenToActualScreenPoint(label.LabelMainObject.localPosition),
                    label.Agent.ColorMaterial.color, 3f);
            }
            
            foreach (var label in _activeLeft) {
                Drawing.DrawLine(LocalScreenToActualScreenPoint(label.AnchorScreenPosition),
                    LocalScreenToActualScreenPoint(label.LabelMainObject.localPosition),
                    label.Agent.ColorMaterial.color, 3f);
            }
        }

        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        private void PickActiveLabels() {
            var frustumPlanes = _mainCamera.CurrentFrustumPlanes;

            // 1. selecting all labels where the Agent is a target, Agent is active, the AnchorPoint is in the left half
            //    of the screen, the AnchorPoint is inside the screen and the AnchorPoint is inside of the detection
            //    circle
            // 2. ordering the list based on distance from the center
            // 3. ordering the list based on absolute y-value (from center out on y-axis)
            // 4. taking the first _maxLabelsPerSide labels (these will be displayed)
            // 5. ordering those labels by their y-value (bottom to top of the screen)
            // 6. converting the enumerable to a list
            var leftLabelsActive = AllLabels
                .Where(l => l.Agent.IsTarget() && l.AnchorScreenPosition.x < 0 && l.Agent.Model.activeSelf &&
                            GeometryUtility.TestPlanesAABB(frustumPlanes, new Bounds(l.Agent.GetAnchorPoint(), Vector3.one * .05f)) &&
                            Vector2.Distance(Vector2.zero, l.AnchorScreenPosition) <= _detectionRadius)
                .OrderBy(l => Vector2.Distance(Vector2.zero, l.AnchorScreenPosition))
                .ThenBy(l => Mathf.Abs(l.AnchorScreenPosition.y)).Take(_maxLabelsPerSide)
                .OrderBy(l => l.AnchorScreenPosition.y).ToList();

            // see the left label ordering, done the same way
            var rightLabelsActive = AllLabels
                .Where(l => l.Agent.IsTarget() && l.AnchorScreenPosition.x >= 0 && l.Agent.Model.activeSelf &&
                            GeometryUtility.TestPlanesAABB(frustumPlanes, new Bounds(l.Agent.GetAnchorPoint(), Vector3.one * .05f)) &&
                            Vector2.Distance(Vector2.zero, l.AnchorScreenPosition) <= _detectionRadius)
                .OrderBy(l => Vector2.Distance(Vector2.zero, l.AnchorScreenPosition))
                .ThenBy(l => Mathf.Abs(l.AnchorScreenPosition.y)).Take(_maxLabelsPerSide)
                .OrderBy(l => l.AnchorScreenPosition.y).ToList();

            var pointsInactive = AllLabels.Except(leftLabelsActive).Except(rightLabelsActive);

            foreach (var screenLabel in pointsInactive) {
                screenLabel.Deactivate();
            }

            PlaceLabels(ref leftLabelsActive, true);
            PlaceLabels(ref rightLabelsActive, false);

            _activeLeft = leftLabelsActive.ToArray();
            _activeRight = rightLabelsActive.ToArray();
        }

        private void PlaceLabels(ref List<ScreenLabel> activeLabels, bool left) {
            if (activeLabels.Count == 0) return;
            var preferredY = new float[activeLabels.Count];

            var firstPreferredPoint = AnchorToPlacementCircle(activeLabels[0]);
            
            // checking if the lower edge of the label would be below the lower screen edge
            if (firstPreferredPoint.y - _labelHeight / 2f - Padding < -MaxHeight / 2f) {
                preferredY[0] = _lowestY;
            } else {
                preferredY[0] = firstPreferredPoint.y;
            }

            for (var i = 1; i < preferredY.Length; i++) {
                var preferredPoint = AnchorToPlacementCircle(activeLabels[i]);

                if (preferredPoint.y - _labelHeight / 2f  < preferredY[i - 1] + _labelHeight / 2f + Padding) {
                    var newY = preferredY[i - 1] + _labelHeight + Padding;
                    preferredY[i] = newY;
                } else {
                    preferredY[i] = preferredPoint.y;
                }
            }

            var shiftDown = 0f;
            if (preferredY[preferredY.Length - 1] + _labelHeight / 2f > MaxHeight / 2f) {
                shiftDown = preferredY[preferredY.Length - 1] + _labelHeight / 2f - MaxHeight / 2f;
            }
            
            for (var i = 0; i < activeLabels.Count; i++) {
                activeLabels[i].Activate();
                var pos = new Vector2((left ? -1 : 1) * GetXForY(preferredY[i] - shiftDown), preferredY[i] - shiftDown);
                activeLabels[i].LabelMainObject.localPosition = pos;
            }
        }

        private float GetXForY(float y) {
            // clamping the y value
            if (Mathf.Abs(y) > MaxHeight / 2f) {
                y = Mathf.Sign(y) * MaxHeight / 2f;
            }

            // using a parabola to have a slight curve for placing the labels
            return MaxWidth / 2f - _labelWidth / 2f - .0001f * Mathf.Pow(y, 2) + 60f;
        }

        private Vector2 AnchorToPlacementCircle(ScreenLabel label) {
            return new Vector2(GetXForY(label.AnchorScreenPosition.y), label.AnchorScreenPosition.y);
        }

        public Vector2 WorldToScreenPoint(Vector3 point) {
            var wts = _mainCamera.Camera.WorldToScreenPoint(point);
            
            return new Vector2(Mathf.Lerp(-MaxWidth / 2f, MaxWidth / 2f, wts.x / Screen.width),
                Mathf.Lerp(-MaxHeight / 2f, MaxHeight / 2f, wts.y / Screen.height));
        }

        private Vector2 LocalScreenToActualScreenPoint(Vector2 point) {
            return new Vector2(Mathf.Lerp(0, Screen.width, (point.x + MaxWidth / 2f) / MaxWidth),
                Mathf.Lerp(0, Screen.height, (point.y - MaxHeight / 2f) / -MaxHeight));
        }
    }
}
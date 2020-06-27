﻿using UnityEngine;

namespace Scenery.RoadNetwork.RoadGeometries {
    public class LineGeometry : RoadGeometry {
        
        public LineGeometry(float sStart, float x, float y, float hdg, float length) : base(sStart, x,
            y, hdg, length) { }
        
        public override Vector2 Evaluate(float s, float t) {
            var offset = new Vector2(s, t);
            offset.RotateRadians(hdg);
            offset.x += x;
            offset.y += y;

            return offset;
        }

        public override float EvaluateHeading(float s) {
            return hdg;
        }
    }
}
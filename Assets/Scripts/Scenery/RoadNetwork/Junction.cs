﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scenery.RoadNetwork {
    
    /// <summary>
    /// Class representing a Junction from OpenDrive
    /// </summary>
    public class Junction : VisualizationElement {
        
        /// <summary>
        /// Dictionary containing all Roads based on their id
        /// </summary>
        public Dictionary<string, Road> Roads { get; } = new();

        /// <summary>
        /// The RoadDesign to be used for displaying the road network
        /// </summary>
        public RoadDesign RoadDesign { get; set; }
        
        /// <summary>
        /// The lowest point for Roads on this Junction, gets set in DisplaceAllLanesAndRoadMarks()
        /// </summary>
        public float LowestPoint { get; private set; }
        
        /// <summary>
        /// The Connections of this Junction
        /// </summary>
        public List<Connection> Connections { get; } = new();

        /// <summary>
        /// Adds a Road to this Junction
        /// </summary>
        /// <param name="road">The road object to be added</param>
        public void AddRoad(Road road) {
            Roads.Add(road.Id, road);
        }

        /// <summary>
        /// Moves all Roads in the hierarchy underneath this Junction object
        /// </summary>
        public void ParentAllRoads() {
            foreach (var road in Roads.Values) {
                road.transform.parent = transform;
            }
        }

        /// <summary>
        /// Finds all connections for an incoming Road
        /// </summary>
        /// <param name="incomingRoadId">The id of the Road leading to the junction</param>
        /// <returns>A list containing all connections for the incoming Road</returns>
        public List<Connection> GetAllConnectionForIncomingRoad(string incomingRoadId) {
            return Connections.Where(c => c.IncomingRoadOdId == incomingRoadId).ToList();
        }

        /// <summary>
        /// Moves all Lanes and RoadMarks on this Junction on the y-Axis to ensure that there is no overlapping of
        /// textures. The displacement is very small so no shadow artifacts are created. RoadMarks are moved upwards,
        /// Lanes downwards.
        /// </summary>
        public void DisplaceAllLanesAndRoadMarks() {
            var allLanes = new Dictionary<LaneType, List<Lane>>();
            foreach (var section in Roads.SelectMany(road => road.Value.LaneSections)) {
                var lanes = section.LaneIdMappings.Values.ToList();
                foreach (var lane in lanes) {
                    if (!allLanes.ContainsKey(lane.LaneType))
                        allLanes.Add(lane.LaneType, new List<Lane>());
                    allLanes[lane.LaneType].Add(lane);
                }
            }

            float highestDrivingLane = 0;
            if (allLanes.ContainsKey(LaneType.Driving)) {
                for (var index = 0; index < allLanes[LaneType.Driving].Count; index++) {
                    var drivingLane = allLanes[LaneType.Driving][index];
                    drivingLane.transform.position += new Vector3(0, index * RoadDesign.offsetHeight, 0);
                    highestDrivingLane = index * RoadDesign.offsetHeight;
                }
            }

            var currentOffsetIndex = 0;
            foreach (var laneType in new[]{LaneType.Sidewalk, LaneType.Sidewalk, LaneType.Biking, LaneType.Restricted, LaneType.Border, LaneType.Shoulder}) {
                if (!allLanes.ContainsKey(laneType)) continue;
                foreach (var lane in allLanes[laneType]) {
                    lane.transform.position -= new Vector3(0, currentOffsetIndex * RoadDesign.offsetHeight, 0);
                    currentOffsetIndex++;
                }
            }
            LowestPoint = currentOffsetIndex * RoadDesign.offsetHeight;

            var allRoadMarks = new List<RoadMark>();
            foreach (var section in Roads.SelectMany(road => road.Value.LaneSections)) {
                allRoadMarks.AddRange(section.LaneIdMappings.Select(entry => entry.Value.RoadMark));
            }

            var ordered2 = allRoadMarks.OrderByDescending(rm => rm.ParentLane.Parent.CompletelyOnLineSegment)
                .ThenBy(rm => rm.ParentLane.LaneType).ToList();

            for (var i = 0; i < ordered2.Count; i++) {
                ordered2[i].transform.position += new Vector3(0, highestDrivingLane + (i + 1) * RoadDesign.offsetHeight, 0);
            }
        }

        public override ElementOrigin ElementOrigin => ElementOrigin.OpenDrive;
    }

    /// <summary>
    /// Class representing a connection on a Junction.
    /// </summary>
    public class Connection {
        
        /// <summary>
        /// Incoming Road
        /// </summary>
        public string IncomingRoadOdId { get; set; }
        
        /// <summary>
        /// Road on the Junction
        /// </summary>
        public string ConnectingRoadOdId { get; set; }
        
        /// <summary>
        /// ContactPoint of this Connection
        /// </summary>
        public ContactPoint ContactPoint { get; set; }

        /// <summary>
        /// List of all LaneLinks in this Connection
        /// </summary>
        public List<LaneLink> LaneLinks { get; set; } = new();
    }

    /// <summary>
    /// Class representing a link of Lanes in a Connection
    /// </summary>
    public class LaneLink {
        
        /// <summary>
        /// The From-Lane
        /// </summary>
        public string From { get; set; }
        
        /// <summary>
        /// The To-Lane
        /// </summary>
        public string To { get; set; }
    }
}
﻿
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;
using Visualization;
using Visualization.POIs;

namespace Importer.XMLHandlers {

    public class ValueMapper {
        private readonly Dictionary<string, int> _defaultPositionMapping = new();
        private readonly Dictionary<string, int> _listPositionMapping = new();
        private readonly Dictionary<string, int> _tuplePositionMapping = new();

        private string[] _currentSplitSample;
        
        public ValueMapper(string header) {
            var split = header.Split(new[] { "," }, StringSplitOptions.None);

            for (var index = 0; index < split.Length; index++) {
                var part = split[index];
                if (part.Contains("[{")) {
                    // list element
                    var listSplit = part.Split(new[] { "[{" }, StringSplitOptions.None);
                    _listPositionMapping.Add(listSplit[0].Replace(" ", ""), index);
                } else if (part.Contains("{")) {
                    var tupleSplit = part.Split(new[] { "{" }, StringSplitOptions.None);
                    _tuplePositionMapping.Add(tupleSplit[0].Replace(" ", ""), index);
                } else {
                    // default element
                    _defaultPositionMapping.Add(part.Replace(" ", ""), index);
                }
            }
        }

        public void SetCurrentSample(string sample) {
            _currentSplitSample = sample.Split(',');
        }
        
        public string GetString(string name) {
            return _currentSplitSample[_defaultPositionMapping[name]];
        }
        
        public float GetFloat(string name) {
            return float.Parse(_currentSplitSample[_defaultPositionMapping[name]], CultureInfo.InvariantCulture);
        }

        public List<string> GetList(string name) {
            var list = _currentSplitSample[_listPositionMapping[name]];
            list = list.Replace("[", "").Replace("]", "");

            var listSplit = list.Split(new[] { "}{" }, StringSplitOptions.None);

            return listSplit.Select(element => element.Replace("{", "").Replace("}", "").Replace(" ", ""))
                .Where(cleaned => !string.IsNullOrEmpty(cleaned)).ToList();
        }

        public ITuple GetTuple(string name) {
            if (!_tuplePositionMapping.ContainsKey(name))
                return null;
            
            var tuple = _currentSplitSample[_tuplePositionMapping[name]];
            tuple = tuple.Replace("{", "").Replace("}", "");
            var tupleSplit = tuple.Split(new[] { "|" }, StringSplitOptions.None);

            return tupleSplit.Length switch {
                1 => new Tuple<float>(float.Parse(tupleSplit[0], CultureInfo.InvariantCulture)),
                2 => new Tuple<float, float>(float.Parse(tupleSplit[0], CultureInfo.InvariantCulture),
                    float.Parse(tupleSplit[1], CultureInfo.InvariantCulture)),
                3 => new Tuple<float, float, float>(float.Parse(tupleSplit[0], CultureInfo.InvariantCulture),
                    float.Parse(tupleSplit[1], CultureInfo.InvariantCulture),
                    float.Parse(tupleSplit[2], CultureInfo.InvariantCulture)),
                _ => null
            };
        }
    }
    
    public class DReaMOutputXmlHandler : XmlHandler {
        
        private Dictionary<int, List<string>> _agentValueMapping;

        public List<IntersectionStoppingPoints> StoppingPoints = new();

        public Dictionary<string, List<ConflictAreaInfo>> ConflictAreaMapping { get; } = new();

        private ValueMapper _valueMapper;

        private int _minSampleTime;
        private int _maxSampleTime;
        
        public string RunId { get; set; }

        private XElement _runResult;

        public void SetSampleTimeLimits(int min, int max) {
            _minSampleTime = min;
            _maxSampleTime = max;
        }

        public override XmlType GetXmlType() => XmlType.DReaM;

        public void StartImport() {
            if (xmlDocument.Root == null)
                throw new Exception("XML is not formatted correctly.");

            var allRunResults = xmlDocument.Root.Element("RunResults")?.Elements("RunResult");
            if (allRunResults == null) {
                throw new ArgumentMissingException("No RunResults found in DReaMOutput. Aborting.");
            } 
            foreach (var runResult in allRunResults) {
                if (!GetString(runResult, "RunId").Contains(RunId)) continue;
                _runResult = runResult;
                break;
            }
            
            if (_runResult == null)
                throw new XmlException("No RunResult with the same id as SimulationOutput found!");

            var infrastructureDataElement = xmlDocument.Root.Element("InfrastructureData");
            if (infrastructureDataElement != null) {
                ImportStoppingPoints(infrastructureDataElement);
                ImportConflictAreas(infrastructureDataElement);
            } else {
                Debug.Log(
                    "No InfrastructureData found in DReaMOutput, skipping display of Stopping Points and Conflict Areas.");
            }
            
            PrepareValueMapping();
            ImportSamples();
        }

        private void ImportSamples() {
            var samples = _runResult.Element("Cyclics")?.Element("Samples")?.Elements("Sample") ??
                          throw new ArgumentMissingException("Samples not given correctly.");

            foreach (var sample in samples) {
                var sampleTime = GetInt(sample, "time", -1);
                if (sampleTime < 0) 
                    throw new ArgumentMissingException("Negative time values are not supported.");
                if (sampleTime < _minSampleTime || sampleTime > _maxSampleTime) {
                    Debug.Log("Continuing due to " + sampleTime);
                    continue;
                }

                var sampleAgents = sample.Elements("Agent").ToList();

                foreach (var sampleAgent in sampleAgents) {
                    var id = GetString(sampleAgent, "id", "-1");
                    var agent = VisualizationMaster.Instance.Agents.First(x => x.Id == id);

                    var agentSample = string.Concat(sampleAgent.Nodes());
                    _valueMapper.SetCurrentSample(agentSample);

                    var step = agent.SimulationSteps[sampleTime];
                    var info = step.AdditionalInformation;

                    // extract basic string info
                    info.ScanAoI = _valueMapper.GetString("ScanAOI");
                    step.AllInfo.Add("ScanAOI", info.ScanAoI);
                    info.GlanceType = _valueMapper.GetString("GazeType");
                    step.AllInfo.Add("GazeType", info.GlanceType);
                    info.CrossingPhase = _valueMapper.GetString("crossingPhase");
                    step.AllInfo.Add("crossingPhase", info.CrossingPhase);

                    // extract sensor information
                    var sensorInformation = new SensorInformation {
                        Distance = _valueMapper.GetFloat("viewDistance"),
                        Heading = _valueMapper.GetFloat("ufovAngle"),
                        OpeningAngle = _valueMapper.GetFloat("openingAngle")
                    };
                    
                    step.AllInfo.Add("viewDistance", sensorInformation.Distance);
                    step.AllInfo.Add("ufovAngle", sensorInformation.Heading);
                    step.AllInfo.Add("openingAngle", sensorInformation.OpeningAngle);

                    step.SensorInformation.Add("driver", sensorInformation);

                    // extract other agent information
                    var otherAgents = new List<Tuple<string, Vector2, float>>();
                    foreach (var otherAgentString in _valueMapper.GetList("otherAgents")) {
                        var split = otherAgentString.Split(new[] { "|" }, StringSplitOptions.None);

                        var otherId = split[0];
                        var posX = float.Parse(split[1], CultureInfo.InvariantCulture);
                        var posY = float.Parse(split[2], CultureInfo.InvariantCulture);
                        var hdg  = float.Parse(split[3], CultureInfo.InvariantCulture);
                        otherAgents.Add(new Tuple<string, Vector2, float>(otherId, new Vector2(posX, posY), hdg));
                    }

                    info.OtherAgents = otherAgents.ToArray();
                    step.AllInfo.Add("otherAgents", otherAgents.ToArray());
                    
                    // get (global) position of UFOV
                    var startPosUfov = _valueMapper.GetTuple("startPosUFOV");
                    if (startPosUfov is Tuple<float, float> startPosTuple) {
                        if (step.SensorInformation.TryGetValue("driver", out var driverSensor)) {
                            driverSensor.GlobalPosition = new Vector2(startPosTuple.Item1, startPosTuple.Item2);
                        }
                    }
                }
            }
        }

        private void ImportConflictAreas(XContainer infrastructureData) {
            var caElement = infrastructureData.Element("ConflictAreas");
            if (caElement == null) {
                Debug.Log("No ConflictAreas found in DReaMOutput.");
                return;
            }
            var caJunctions = caElement.Elements("Junction").ToArray();
            if (!caJunctions.Any()) {
                Debug.Log("No Junctions found for ConflictAreas in DReaMOutput.");
                return;
            }

            foreach (var junction in caJunctions) {
                var junctionId = junction.Attribute("id")?.Value.ToLower() ?? "not on junction";
                if (!ConflictAreaMapping.ContainsKey(junctionId))
                    ConflictAreaMapping.Add(junctionId, new List<ConflictAreaInfo>());

                var conflictAreas = junction.Elements("ConflictArea");
                foreach (var conflictArea in conflictAreas) {
                    ConflictAreaMapping[junctionId].Add(new ConflictAreaInfo {
                        startSa = GetFloat(conflictArea, "startA"),
                        endSa = GetFloat(conflictArea, "endA"),
                        startSb = GetFloat(conflictArea, "startB"),
                        endSb = GetFloat(conflictArea, "endB"),
                        roadIdA = GetString(conflictArea, "roadA"),
                        laneIdA = GetInt(conflictArea, "laneA"),
                        roadIdB = GetString(conflictArea, "roadB"),
                        laneIdB = GetInt(conflictArea, "laneB")
                    });
                }
            }
        }

        private void ImportStoppingPoints(XContainer infrastructureData) {
            var spElement = infrastructureData.Element("StoppingPoints");
            if (spElement == null) {
                Debug.Log("No StoppingPoints found in DReaMOutput.");
                return;
            }
            var spJunctions = spElement.Elements("Junction").ToArray();
            if (!spJunctions.Any()) {
                Debug.Log("No Junctions found for StoppingPoints in DReaMOutput.");
                return;
            }

            foreach (var junction in spJunctions) {
                var intersectionId = GetString(junction, "id", "UNKNOWN");
                var isp = new IntersectionStoppingPoints
                    { IntersectionId = intersectionId, laneStoppingPoints = new List<LaneStoppingPoints>() };

                var lanes = junction.Elements("Lane");
                foreach (var lane in lanes) {
                    var lsp = new LaneStoppingPoints {
                        LaneId = GetString(lane, "id", "UNKNOWN"),
                        stoppingPoints = new List<StoppingPoint>()
                    };

                    var points = lane.Elements("Point").ToArray();
                    if (!points.Any())
                        continue;

                    foreach (var stoppingPoint in points) {
                        lsp.stoppingPoints.Add(new StoppingPoint {
                            position = new Vector2(GetFloat(stoppingPoint, "posX"), GetFloat(stoppingPoint, "posY")),
                            roadId = GetString(stoppingPoint, "road"),
                            laneId = GetString(stoppingPoint, "lane"),
                            type = GetString(stoppingPoint, "type")
                        });
                    }

                    isp.laneStoppingPoints.Add(lsp);
                }

                StoppingPoints.Add(isp);
            }
        }

        private void PrepareValueMapping() {
            var header = _runResult.Element("Cyclics")?.Element("Header") ??
                         throw new ArgumentMissingException("Simulation Output contains no Cyclics.");

            _valueMapper = new ValueMapper(string.Concat(header.Nodes()));
        }
    }
}
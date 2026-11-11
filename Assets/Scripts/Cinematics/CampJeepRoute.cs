using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FEB.Cinematics
{
    [DisallowMultipleComponent]
    public sealed class CampJeepRoute : MonoBehaviour
    {
        [SerializeField, Tooltip("Do exterior até a vaga. O último marco é a vaga, com o eixo azul apontando para a frente do jipe.")]
        Transform[] chegada;
        [SerializeField, Tooltip("Da mesma vaga até o exterior. O primeiro trecho sai de frente da vaga.")]
        Transform[] partida;

        const float SampleSpacing = .5f;
        const float Acceleration = 1.6f;
        const float Braking = 2.8f;

        public bool IsValid(bool arriving)
        {
            var marks = arriving ? chegada : partida;
            if (marks == null || marks.Length < 2 || !marks[0]) return false;
            for (int i = 1; i < marks.Length; i++)
                if (!marks[i] || Horizontal(marks[i].position - marks[i - 1].position).sqrMagnitude < .01f) return false;
            var dock = arriving ? marks[^1] : marks[0];
            Vector3 direction = arriving ? dock.position - marks[^2].position : marks[1].position - dock.position;
            return Vector3.Dot(Horizontal(dock.forward), Horizontal(direction).normalized) > .1f;
        }

        public bool TryGetStartPose(bool arriving, out Pose pose)
        {
            pose = Pose.identity;
            if (!IsValid(arriving)) return false;
            var marks = arriving ? chegada : partida;
            Vector3 direction = arriving ? marks[1].position - marks[0].position : marks[0].forward;
            pose = GroundPose(marks[0].position, direction, Terrain.activeTerrains);
            return true;
        }

        public IEnumerator Drive(Transform jeep, bool arriving, float speed, Transform player = null)
        {
            if (!jeep || !IsValid(arriving)) yield break;
            var marks = arriving ? chegada : partida;
            var road = BuildRoad(marks, arriving);
            float[] distances = new float[road.Count];
            float[] limits = new float[road.Count];
            speed = Mathf.Max(.1f, speed);
            for (int i = 1; i < road.Count; i++)
                distances[i] = distances[i - 1] + Horizontal(road[i] - road[i - 1]).magnitude;

            // Limites ao quadrado permitem frear antes de cada curva e parar exatamente na vaga.
            for (int i = road.Count - 2; i >= 0; i--)
            {
                float segment = distances[i + 1] - distances[i];
                float speedSquared = speed * speed;
                if (i > 0)
                {
                    float turn = Vector3.Angle(Horizontal(road[i] - road[i - 1]), Horizontal(road[i + 1] - road[i])) * Mathf.Deg2Rad;
                    float span = (distances[i + 1] - distances[i - 1]) * .5f;
                    if (turn > .005f) speedSquared = Mathf.Min(speedSquared, 2.5f * span / turn);
                }
                limits[i] = Mathf.Min(speedSquared, limits[i + 1] + 2 * Braking * segment);
            }

            var terrains = Terrain.activeTerrains;
            float distance = 0f, velocity = 0f;
            int segmentIndex = 0;
            Pose pose = GroundPose(road[0], road[1] - road[0], terrains);
            jeep.SetPositionAndRotation(pose.position, pose.rotation);
            while (distances[^1] - distance > .003f)
            {
                int next = segmentIndex + 1;
                float fraction = Mathf.InverseLerp(distances[segmentIndex], distances[next], distance);
                float speedLimit = Mathf.Sqrt(Mathf.Lerp(limits[segmentIndex], limits[next], fraction));
                float clearance = ClearanceToPlayer(jeep, player);
                speedLimit = Mathf.Min(speedLimit, Mathf.Sqrt(2 * Braking * clearance));
                velocity = Mathf.MoveTowards(velocity, speedLimit, (velocity < speedLimit ? Acceleration : Braking) * Time.deltaTime);
                distance = Mathf.Min(distance + Mathf.Min(velocity * Time.deltaTime, clearance), distances[^1]);
                while (segmentIndex + 2 < road.Count && distance >= distances[segmentIndex + 1]) segmentIndex++;
                next = segmentIndex + 1;
                fraction = Mathf.InverseLerp(distances[segmentIndex], distances[next], distance);
                Vector3 position = Vector3.Lerp(road[segmentIndex], road[next], fraction);
                Vector3 forward = Vector3.Lerp(Tangent(road, segmentIndex), Tangent(road, next), fraction);
                pose = GroundPose(position, forward, terrains);
                jeep.SetPositionAndRotation(pose.position, pose.rotation);
                yield return null;
            }
            Vector3 endDirection = arriving ? marks[^1].forward : Tangent(road, road.Count - 1);
            pose = GroundPose(road[^1], endDirection, terrains);
            jeep.SetPositionAndRotation(pose.position, pose.rotation);
        }

        static float ClearanceToPlayer(Transform jeep, Transform player)
        {
            if (!player || player.IsChildOf(jeep)) return float.PositiveInfinity;
            Vector3 local = Quaternion.Inverse(jeep.rotation) * (player.position - jeep.position);
            if (local.z < 0 || Mathf.Abs(local.x) > 1.3f || Mathf.Abs(local.y) > 2.3f) return float.PositiveInfinity;
            return Mathf.Max(0, local.z - 2.35f);
        }

        static List<Vector3> BuildRoad(Transform[] marks, bool arriving)
        {
            var points = new List<Vector3>(marks.Length + 1);
            foreach (var mark in marks) points.Add(mark.position);
            int dock = arriving ? marks.Length - 1 : 0;
            int neighbor = arriving ? dock - 1 : 1;
            Vector3 heading = Horizontal(marks[dock].forward).normalized;
            float stub = Mathf.Min(1.5f, Horizontal(points[neighbor] - points[dock]).magnitude * .25f);
            points.Insert(arriving ? dock : 1, points[dock] + heading * (arriving ? -stub : stub));

            var road = new List<Vector3> { points[0] };
            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector3 corner = points[i];
                Vector3 incoming = points[i - 1] - corner, outgoing = points[i + 1] - corner;
                float inLength = Horizontal(incoming).magnitude, outLength = Horizontal(outgoing).magnitude;
                // Limita o corte de esquina à área entre os marcos, sem ultrapassar o corredor.
                float offset = Mathf.Min(5f, .43f * Mathf.Min(inLength, outLength));
                Vector3 start = corner + incoming * (offset / inLength), end = corner + outgoing * (offset / outLength);
                AddLine(road, start);
                int steps = Mathf.Max(3, Mathf.CeilToInt(2 * offset / SampleSpacing));
                for (int j = 1; j <= steps; j++)
                {
                    float t = (float)j / steps;
                    road.Add(Vector3.Lerp(Vector3.Lerp(start, corner, t), Vector3.Lerp(corner, end, t), t));
                }
            }
            AddLine(road, points[^1]);
            return road;
        }

        static void AddLine(List<Vector3> road, Vector3 end)
        {
            Vector3 start = road[^1];
            int steps = Mathf.Max(1, Mathf.CeilToInt(Horizontal(end - start).magnitude / SampleSpacing));
            for (int j = 1; j <= steps; j++) road.Add(Vector3.Lerp(start, end, (float)j / steps));
        }

        static Vector3 Tangent(List<Vector3> road, int i)
        {
            Vector3 previous = road[Mathf.Max(0, i - 1)], next = road[Mathf.Min(road.Count - 1, i + 1)];
            return Horizontal(next - previous).normalized;
        }

        static Vector3 Horizontal(Vector3 vector) => new(vector.x, 0, vector.z);

        static Pose GroundPose(Vector3 position, Vector3 direction, Terrain[] terrains)
        {
            Vector3 normal = Vector3.up;
            foreach (var terrain in terrains)
            {
                var data = terrain.terrainData;
                Vector3 local = position - terrain.transform.position;
                if (local.x < 0 || local.z < 0 || local.x > data.size.x || local.z > data.size.z) continue;
                float x = local.x / data.size.x, z = local.z / data.size.z;
                position.y = terrain.transform.position.y + data.GetInterpolatedHeight(x, z);
                normal = data.GetInterpolatedNormal(x, z);
                break;
            }
            return new Pose(position, Quaternion.LookRotation(Vector3.ProjectOnPlane(Horizontal(direction), normal), normal));
        }

        void OnDrawGizmosSelected()
        {
            DrawRoute(chegada, true, Color.green);
            DrawRoute(partida, false, Color.cyan);
        }

        void DrawRoute(Transform[] marks, bool arriving, Color color)
        {
            if (!IsValid(arriving)) return;
            Gizmos.color = color;
            var road = BuildRoad(marks, arriving);
            for (int i = 1; i < road.Count; i++) Gizmos.DrawLine(road[i - 1], road[i]);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FEB.Cinematics
{
    // Cada manobra só ocupa a parte da estrada que a viatura ainda vai percorrer.
    internal sealed class CampRoadTraffic : IDisposable
    {
        // O Willys mede 3,43 x 1,66 m. A folga cobre também o intervalo entre amostras da curva.
        const float HalfLength = 2.05f, HalfWidth = 1.04f;
        const int BlockSize = 16;
        static readonly List<CampRoadTraffic> requests = new();
        static NavMeshAgent[] pedestrians;
        static float nextScan;

        readonly struct Footprint
        {
            public readonly Vector2 center, forward, side, extent;
            public readonly float height;

            public Footprint(Vector3 point, Vector3 tangent)
            {
                center = new Vector2(point.x, point.z);
                height = point.y;
                forward = new Vector2(tangent.x, tangent.z).normalized;
                side = new Vector2(forward.y, -forward.x);
                extent = new Vector2(Mathf.Abs(forward.x) * HalfLength + Mathf.Abs(side.x) * HalfWidth,
                    Mathf.Abs(forward.y) * HalfLength + Mathf.Abs(side.y) * HalfWidth);
            }

            float Projection(Vector2 axis) => HalfLength * Mathf.Abs(Vector2.Dot(forward, axis)) +
                                               HalfWidth * Mathf.Abs(Vector2.Dot(side, axis));

            public bool Overlaps(in Footprint other)
            {
                Vector2 delta = other.center - center;
                if (Mathf.Abs(delta.x) > extent.x + other.extent.x || Mathf.Abs(delta.y) > extent.y + other.extent.y ||
                    Mathf.Abs(height - other.height) > 2.3f) return false;
                // Quatro eixos de separação mantêm livres as faixas paralelas.
                return Mathf.Abs(Vector2.Dot(delta, forward)) <= HalfLength + other.Projection(forward) &&
                       Mathf.Abs(Vector2.Dot(delta, side)) <= HalfWidth + other.Projection(side) &&
                       Mathf.Abs(Vector2.Dot(delta, other.forward)) <= HalfLength + Projection(other.forward) &&
                       Mathf.Abs(Vector2.Dot(delta, other.side)) <= HalfWidth + Projection(other.side);
            }

            public bool NearPedestrian(Vector2 position, float radius)
            {
                Vector2 delta = position - center;
                return Mathf.Abs(Vector2.Dot(delta, side)) < HalfWidth + radius &&
                       Mathf.Abs(Vector2.Dot(delta, forward)) < HalfLength + radius;
            }
        }

        // Uma zona corresponde a duas porções contíguas que se cruzam. O mesmo par
        // de trajetos pode ter outros cruzamentos, com preferências independentes.
        sealed class Crossing
        {
            public CampRoadTraffic a, b, owner;
            public int a0, a1, b0, b1;

            public int Start(CampRoadTraffic car) => car == a ? a0 : b0;
            public int End(CampRoadTraffic car) => car == a ? a1 : b1;
            public bool Live => a.Alive && b.Alive && a.segment <= a1 && b.segment <= b1;

            public bool Touches(int aMin, int aMax, int bMin, int bMax) =>
                aMin <= a1 + 1 && aMax >= a0 - 1 && bMin <= b1 + 1 && bMax >= b0 - 1;

            public void Include(int aMin, int aMax, int bMin, int bMax)
            {
                a0 = Mathf.Min(a0, aMin); a1 = Mathf.Max(a1, aMax);
                b0 = Mathf.Min(b0, bMin); b1 = Mathf.Max(b1, bMax);
            }
        }

        void AddCrossings(CampRoadTraffic earlier)
        {
            if (SameRoad(earlier))
            {
                leaders.Add(earlier);
                return;
            }
            var zones = new List<Crossing>();
            for (int i = 0; i < road.Length; i++)
            {
                var current = road[i];
                var envelope = Rect.MinMaxRect(current.center.x - current.extent.x, current.center.y - current.extent.y,
                    current.center.x + current.extent.x, current.center.y + current.extent.y);
                for (int block = 0; block < earlier.blocks.Length; block++)
                {
                    if (!earlier.blocks[block].Overlaps(envelope)) continue;
                    int end = Mathf.Min((block + 1) * BlockSize, earlier.road.Length);
                    for (int j = block * BlockSize; j < end; j++)
                    {
                        if (!current.Overlaps(earlier.road[j])) continue;
                        Crossing zone = null;
                        for (int k = zones.Count - 1; k >= 0; k--)
                        {
                            if (!zones[k].Touches(i, i, j, j)) continue;
                            if (zone == null) zone = zones[k];
                            else
                            {
                                var other = zones[k];
                                zone.Include(other.a0, other.a1, other.b0, other.b1);
                                zones.RemoveAt(k);
                            }
                        }
                        if (zone == null)
                            zones.Add(new Crossing { a = this, b = earlier, a0 = i, a1 = i, b0 = j, b1 = j });
                        else zone.Include(i, i, j, j);
                    }
                }
            }
            foreach (var zone in zones)
            {
                if (earlier.segment > zone.b1) continue;
                // Uma nova rota respeita tanto o veículo dentro da área quanto a
                // reserva já concedida a ele junto a este mesmo cruzamento.
                if (earlier.entered && earlier.segment >= zone.b0 || earlier.OwnsAdjacentZone(zone))
                    zone.owner = earlier;
                earlier.crossings.Add(zone);
                crossings.Add(zone);
            }
        }

        bool SameRoad(CampRoadTraffic other)
        {
            if (road.Length != other.road.Length) return false;
            for (int i = 0; i < road.Length; i++)
                if ((road[i].center - other.road[i].center).sqrMagnitude > .01f) return false;
            return true;
        }

        bool OwnsAdjacentZone(Crossing added)
        {
            float start = distances[added.Start(this)], end = distances[added.End(this)];
            foreach (var zone in crossings)
                if (zone.Live && zone.owner == this && Connected(zone, start, end)) return true;
            return false;
        }

        bool Connected(Crossing zone, float start, float end) =>
            distances[zone.Start(this)] <= end + 1.5f && distances[zone.End(this)] >= start - 1.5f;

        bool Claim(Crossing first)
        {
            // Uma viatura só entra se também puder sair. Quando não cabe uma parada
            // entre dois cruzamentos, obtém todas as preferências juntas, ou espera.
            float start = distances[first.Start(this)], end = distances[first.End(this)];
            bool expanded;
            do
            {
                expanded = false;
                foreach (var z in crossings)
                {
                    if (!z.Live || !Connected(z, start, end)) continue;
                    if (z.owner != null && z.owner != this) return false;
                    float from = distances[z.Start(this)], to = distances[z.End(this)];
                    if (from < start) { start = from; expanded = true; }
                    if (to > end) { end = to; expanded = true; }
                }
            } while (expanded);
            // O seguidor não pode reservar a interseção pela qual o líder ainda vai passar.
            foreach (var leader in leaders)
                if (leader.Alive && leader.traveled <= end + 1f) return false;
            foreach (var z in crossings)
                if (z.Live && Connected(z, start, end)) z.owner = this;
            return true;
        }

        float VehicleClearance()
        {
            float gap = float.PositiveInfinity;
            foreach (var leader in leaders)
                if (leader.Alive) gap = Mathf.Min(gap, Mathf.Max(0f, leader.traveled - traveled - 4.85f));
            Crossing next = null;
            int first = road.Length;
            for (int i = crossings.Count - 1; i >= 0; i--)
            {
                var zone = crossings[i];
                if (!zone.Live) { crossings.RemoveAt(i); continue; }
                int at = zone.Start(this);
                if (zone.owner != this && at < first) { next = zone; first = at; }
            }
            if (next == null) return gap;
            float toEntry = Mathf.Max(0f, distances[first] - traveled - .45f);
            // A preferência nasce perto da linha de espera, e não ao aparecer no mapa.
            if (toEntry > 8f || next.owner != null || !Claim(next)) return Mathf.Min(gap, toEntry);
            return gap;
        }
        readonly Transform jeep;
        readonly Footprint[] road;
        readonly Rect[] blocks;
        readonly float[] distances;
        readonly List<Crossing> crossings = new();
        readonly List<CampRoadTraffic> leaders = new();
        int segment, lastFrame;
        float traveled;
        bool disposed, entered;

        // StopCoroutine/SetActive também podem interromper uma manobra em espera.
        bool Alive => !disposed && jeep && Time.frameCount - lastFrame <= 2;

        CampRoadTraffic(Transform jeep, List<Vector3> points, float[] distances)
        {
            this.jeep = jeep;
            this.distances = distances;
            lastFrame = Time.frameCount;
            road = new Footprint[points.Count];
            for (int i = 0; i < road.Length; i++)
                road[i] = new Footprint(points[i], points[Mathf.Min(i + 1, road.Length - 1)] - points[Mathf.Max(i - 1, 0)]);
            blocks = new Rect[(road.Length + BlockSize - 1) / BlockSize];
            for (int b = 0; b < blocks.Length; b++)
            {
                int start = b * BlockSize;
                Vector2 min = road[start].center - road[start].extent, max = road[start].center + road[start].extent;
                int end = Mathf.Min(start + BlockSize, road.Length);
                for (int i = start + 1; i < end; i++)
                {
                    min = Vector2.Min(min, road[i].center - road[i].extent);
                    max = Vector2.Max(max, road[i].center + road[i].extent);
                }
                blocks[b] = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
        }

        public static CampRoadTraffic Request(Transform jeep, List<Vector3> points, float[] distances)
        {
            requests.RemoveAll(r => !r.Alive || r.jeep == jeep);
            var request = new CampRoadTraffic(jeep, points, distances);
            foreach (var earlier in requests)
                if (earlier.jeep.gameObject.scene == jeep.gameObject.scene)
                    request.AddCrossings(earlier);
            requests.Add(request);
            return request;
        }

        public bool MayEnter()
        {
            lastFrame = Time.frameCount;
            entered = VehicleClearance() > .001f;
            return entered;
        }

        public float Clearance(Transform player, int index, float traveled, float speed)
        {
            lastFrame = Time.frameCount;
            segment = index;
            this.traveled = traveled;
            float clearance = VehicleClearance();

            // A busca curta acompanha as curvas, sem frear por pessoas nas barracas ao lado.
            float ahead = 3f + speed * speed / 5.6f;
            int end = index;
            while (end < road.Length - 1 && distances[end] - traveled < ahead) end++;
            if (player && !player.IsChildOf(jeep))
                clearance = Mathf.Min(clearance, DistanceToPedestrian(player.position, .34f, index, end, traveled));

            if (pedestrians == null || Time.unscaledTime >= nextScan)
            {
                pedestrians = UnityEngine.Object.FindObjectsByType<NavMeshAgent>();
                nextScan = Time.unscaledTime + 1f;
            }
            foreach (var agent in pedestrians)
            {
                if (!agent || !agent.isActiveAndEnabled || agent.gameObject.scene != jeep.gameObject.scene) continue;
                Vector3 delta = agent.transform.position - jeep.position;
                if (delta.sqrMagnitude > (ahead + 4f) * (ahead + 4f) || agent.transform.IsChildOf(jeep)) continue;
                clearance = Mathf.Min(clearance, DistanceToPedestrian(agent.transform.position, agent.radius, index, end, traveled));
            }
            return clearance;
        }

        float DistanceToPedestrian(Vector3 point, float radius, int index, int end, float traveled)
        {
            if (Mathf.Abs(point.y - jeep.position.y) > 2.3f) return float.PositiveInfinity;
            Vector2 position = new(point.x, point.z);
            for (int i = index; i <= end; i++)
            {
                if (!road[i].NearPedestrian(position, radius)) continue;
                // Alguém saindo por trás do jipe já está fora da direção de marcha.
                if (Vector2.Dot(position - road[i].center, road[i].forward) < 0f) continue;
                return Mathf.Max(0f, distances[i] - traveled - .26f);
            }
            return float.PositiveInfinity;
        }

        public void Dispose()
        {
            disposed = true;
            requests.Remove(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            requests.Clear();
            pedestrians = null;
            nextScan = 0f;
        }
    }
}

export const DIRECTIONS = [
  { x: 1, y: 0, name: "東" },
  { x: 0.5, y: 0.8660254, name: "南東" },
  { x: -0.5, y: 0.8660254, name: "南西" },
  { x: -1, y: 0, name: "西" },
  { x: -0.5, y: -0.8660254, name: "北西" },
  { x: 0.5, y: -0.8660254, name: "北東" }
];

const clamp = (v, min, max) => Math.max(min, Math.min(max, v));
const mix = (a, b, t) => a + (b - a) * t;

export class CrustSimulation {
  constructor(width = 36, height = 22, seed = Date.now()) {
    this.width = width;
    this.height = height;
    this.seed = seed >>> 0;
    this.stepCount = 0;
    this.seaLevel = 0;
    this.volcanicStrength = 1;
    this.volcanicRadius = 1;
    this.boundaryInfluence = 0.18;
    this.erosionRate = 0.08;
    this.erosionRange = 2;
    this.volcanoes = [];
    this.events = [];
    this.cells = [];
    this.reset(this.seed);
  }

  random() {
    this.seed = (1664525 * this.seed + 1013904223) >>> 0;
    return this.seed / 4294967296;
  }

  index(q, r) {
    if (r < 0 || r >= this.height) return -1;
    q = (q % this.width + this.width) % this.width;
    return r * this.width + q;
  }

  coords(index) { return { q: index % this.width, r: Math.floor(index / this.width) }; }

  neighborIndex(q, r, direction) {
    const odd = r & 1;
    const offsets = odd
      ? [[1, 0], [1, 1], [0, 1], [-1, 0], [0, -1], [1, -1]]
      : [[1, 0], [0, 1], [-1, 1], [-1, 0], [-1, -1], [0, -1]];
    const [dq, dr] = offsets[direction];
    return this.index(q + dq, r + dr);
  }

  neighbors(index) {
    const { q, r } = this.coords(index);
    return DIRECTIONS.map((_, d) => this.neighborIndex(q, r, d)).filter(i => i >= 0);
  }

  reset(seed = Date.now()) {
    this.seed = seed >>> 0;
    this.initialSeed = this.seed;
    this.stepCount = 0;
    this.events = [];
    const driftCenters = Array.from({ length: 9 }, () => ({
      q: this.random() * this.width,
      r: this.random() * this.height,
      angle: this.random() * Math.PI * 2,
      continental: this.random() > 0.52
    }));
    this.cells = Array.from({ length: this.width * this.height }, (_, index) => {
      const { q, r } = this.coords(index);
      let closest = driftCenters[0];
      let best = Infinity;
      for (const center of driftCenters) {
        const dx = this.wrapDelta(q - center.q, this.width);
        const dy = r - center.r;
        const distance = dx * dx + dy * dy;
        if (distance < best) { best = distance; closest = center; }
      }
      const warp = Math.sin(q * 0.73 + r * 0.31) + Math.cos(r * 0.67 - q * 0.19);
      const continental = closest.continental && best < 75 + warp * 12;
      const thickness = continental ? 30 + this.random() * 18 : 6 + this.random() * 7;
      const basement = continental ? -12 - this.random() * 8 : -8 - this.random() * 5;
      const speed = 0.55 + this.random() * 0.55;
      return {
        crust: {
          thickness,
          basement,
          density: continental ? 2.64 + this.random() * 0.12 : 2.93 + this.random() * 0.16,
          age: this.random() * (continental ? 1600 : 180),
          temperature: 180 + this.random() * 420,
          sediment: 0,
          velocity: { x: Math.cos(closest.angle) * speed, y: Math.sin(closest.angle) * speed },
          nextDirection: 0
        },
        plateId: 0,
        collision: false,
        newborn: false
      };
    });
    this.volcanoes = Array.from({ length: 3 }, () => ({
      q: Math.floor(this.random() * this.width),
      r: Math.floor(this.random() * this.height),
      strength: 0.7 + this.random() * 0.6,
      radius: 5 + this.random() * 3
    }));
    this.rebuildPlates();
    this.log("惑星を生成", `${this.plateCount}個の運動領域を検出`);
  }

  wrapDelta(delta, size) {
    if (delta > size / 2) return delta - size;
    if (delta < -size / 2) return delta + size;
    return delta;
  }

  position(q, r) { return { x: q + (r & 1) * 0.5, y: r * 0.8660254 }; }

  volcanicForce(q, r) {
    const p = this.position(q, r);
    let x = 0, y = 0, heat = 0;
    for (const volcano of this.volcanoes) {
      const v = this.position(volcano.q, volcano.r);
      const dx = this.wrapDelta(p.x - v.x, this.width);
      const dy = p.y - v.y;
      const distance = Math.max(0.35, Math.hypot(dx, dy));
      const radius = Math.max(0.25, volcano.radius * this.volcanicRadius);
      const falloff = Math.exp(-distance / radius) * volcano.strength * this.volcanicStrength;
      x += dx / distance * falloff;
      y += dy / distance * falloff;
      heat += falloff;
    }
    return { x, y, heat };
  }

  nearestDirection(vector) {
    let best = 0, bestDot = -Infinity;
    const length = Math.hypot(vector.x, vector.y) || 1;
    DIRECTIONS.forEach((direction, i) => {
      const dot = (vector.x * direction.x + vector.y * direction.y) / length;
      if (dot > bestDot) { bestDot = dot; best = i; }
    });
    return best;
  }

  boundaryCorrection(index, velocity) {
    const nearby = this.neighbors(index).map(i => this.cells[i].crust).filter(Boolean);
    if (!nearby.length) return { x: 0, y: 0 };
    const average = nearby.reduce((sum, crust) => ({ x: sum.x + crust.velocity.x, y: sum.y + crust.velocity.y }), { x: 0, y: 0 });
    average.x /= nearby.length;
    average.y /= nearby.length;
    const mismatch = Math.hypot(average.x - velocity.x, average.y - velocity.y);
    const coupling = this.boundaryInfluence * clamp(1 - mismatch / 2.5, -0.35, 1);
    return { x: (average.x - velocity.x) * coupling, y: (average.y - velocity.y) * coupling };
  }

  step() {
    const outgoing = Array.from({ length: this.cells.length }, () => []);
    this.cells.forEach((cell, index) => {
      if (!cell.crust) return;
      const { q, r } = this.coords(index);
      const volcanic = this.volcanicForce(q, r);
      const correction = this.boundaryCorrection(index, cell.crust.velocity);
      const velocity = {
        x: cell.crust.velocity.x * 0.94 + volcanic.x * 0.34 + correction.x,
        y: cell.crust.velocity.y * 0.94 + volcanic.y * 0.34 + correction.y
      };
      const direction = this.nearestDirection(velocity);
      const neighbor = this.neighborIndex(q, r, direction);
      const destination = neighbor >= 0 ? neighbor : index;
      outgoing[destination].push({
        ...cell.crust,
        age: cell.crust.age + 1,
        temperature: Math.max(20, cell.crust.temperature * 0.994 + volcanic.heat * 42),
        velocity,
        nextDirection: direction,
        source: index
      });
    });

    let collisions = 0, births = 0, subductions = 0;
    const next = outgoing.map((incoming, index) => {
      if (incoming.length === 1) return { crust: this.normalizeCrust(incoming[0]), plateId: 0, collision: false, newborn: false };
      if (incoming.length > 1) {
        collisions++;
        const result = this.resolveCollision(incoming);
        if (result.subducted) subductions++;
        return { crust: result.crust, plateId: 0, collision: true, newborn: false };
      }
      if (this.shouldCreateCrust(index)) {
        births++;
        return { crust: this.createYoungCrust(index), plateId: 0, collision: false, newborn: true };
      }
      return { crust: null, plateId: 0, collision: false, newborn: false };
    });
    this.cells = next;
    this.diffuseDeposits();
    this.stepCount++;
    this.rebuildPlates();
    if (collisions) this.log("地殻衝突", `${collisions}地点（沈み込み ${subductions}）`);
    if (births) this.log("地殻生成", `${births}地点で新生地殻が形成`);
    if (this.stepCount % 10 === 0) this.log("領域再構成", `${this.plateCount}プレートを識別`);
    return { collisions, births, subductions, plates: this.plateCount };
  }

  normalizeCrust(crust) {
    const speed = Math.hypot(crust.velocity.x, crust.velocity.y);
    if (speed > 2.4) { crust.velocity.x *= 2.4 / speed; crust.velocity.y *= 2.4 / speed; }
    crust.thickness = clamp(crust.thickness, 2, 80);
    crust.basement = clamp(crust.basement, -45, 8);
    crust.density = clamp(crust.density, 2.4, 3.5);
    crust.sediment = clamp(Number(crust.sediment) || 0, 0, crust.thickness);
    return crust;
  }

  resolveCollision(incoming) {
    const sorted = [...incoming].sort((a, b) => (b.density + 0.22 / b.thickness) - (a.density + 0.22 / a.thickness));
    const sink = sorted[0];
    const upper = sorted[sorted.length - 1];
    const scoreGap = (sink.density + 0.22 / sink.thickness) - (upper.density + 0.22 / upper.thickness);
    const totalThickness = incoming.reduce((sum, crust) => sum + crust.thickness, 0);
    const totalWeight = incoming.reduce((sum, crust) => sum + crust.thickness * crust.density, 0);
    const velocity = incoming.reduce((sum, crust) => ({ x: sum.x + crust.velocity.x * crust.thickness, y: sum.y + crust.velocity.y * crust.thickness }), { x: 0, y: 0 });
    velocity.x /= totalThickness;
    velocity.y /= totalThickness;
    const subducted = scoreGap > 0.075;
    const retained = subducted ? upper.thickness + (totalThickness - upper.thickness) * 0.28 : totalThickness * 0.72;
    const inheritedSediment = incoming.reduce((sum, crust) => sum + (crust.sediment || 0), 0) * (subducted ? 0.65 : 0.8);
    const depositedThickness = subducted ? 2 : 5;
    const crust = {
      thickness: retained + depositedThickness,
      basement: upper.basement + (subducted ? 1.1 : 2.4),
      density: subducted ? upper.density : totalWeight / totalThickness,
      age: incoming.reduce((sum, c) => sum + c.age * c.thickness, 0) / totalThickness,
      temperature: Math.max(...incoming.map(c => c.temperature)) + (subducted ? 110 : 65),
      sediment: inheritedSediment + depositedThickness,
      velocity,
      nextDirection: this.nearestDirection(velocity)
    };
    return { crust: this.normalizeCrust(crust), subducted };
  }

  shouldCreateCrust(index) {
    const { q, r } = this.coords(index);
    const force = this.volcanicForce(q, r);
    if (force.heat > 0.48) return true;
    let divergence = 0;
    for (const neighbor of this.neighbors(index)) {
      const crust = this.cells[neighbor].crust;
      if (!crust) continue;
      const a = this.position(...Object.values(this.coords(neighbor)));
      const b = this.position(q, r);
      const dx = this.wrapDelta(a.x - b.x, this.width);
      const dy = a.y - b.y;
      const length = Math.hypot(dx, dy) || 1;
      divergence += (crust.velocity.x * dx + crust.velocity.y * dy) / length;
    }
    return divergence > 1.65;
  }

  createYoungCrust(index) {
    const { q, r } = this.coords(index);
    const force = this.volcanicForce(q, r);
    const direction = this.nearestDirection(force);
    return {
      thickness: 5.5 + Math.min(4, force.heat * 2), basement: -8.5, density: 3.05,
      age: 0, temperature: 980 + force.heat * 160,
      sediment: 0,
      velocity: { x: force.x || DIRECTIONS[direction].x * 0.3, y: force.y || DIRECTIONS[direction].y * 0.3 },
      nextDirection: direction
    };
  }

  surfaceElevation(crust) {
    if (!crust) return -12;
    return crust.basement + crust.thickness * (3.3 - crust.density) * 0.62;
  }

  rebuildPlates() {
    let plateId = 0;
    const visited = new Uint8Array(this.cells.length);
    for (let start = 0; start < this.cells.length; start++) {
      if (visited[start] || !this.cells[start].crust) continue;
      plateId++;
      const queue = [start];
      visited[start] = 1;
      while (queue.length) {
        const current = queue.pop();
        const a = this.cells[current].crust.velocity;
        const directionBin = this.nearestDirection(a);
        this.cells[current].crust.nextDirection = directionBin;
        this.cells[current].plateId = plateId;
        for (const neighbor of this.neighbors(current)) {
          if (visited[neighbor] || !this.cells[neighbor].crust) continue;
          const b = this.cells[neighbor].crust.velocity;
          if (this.nearestDirection(b) === directionBin) { visited[neighbor] = 1; queue.push(neighbor); }
        }
      }
    }
    this.plateCount = plateId;
  }

  diffuseDeposits() {
    const rate = clamp(this.erosionRate, 0, 0.5);
    const range = clamp(Math.round(this.erosionRange), 1, 6);
    if (rate <= 0) return;
    const elevation = this.cells.map(cell => this.surfaceElevation(cell.crust));
    const thicknessDelta = new Float64Array(this.cells.length);
    const sedimentDelta = new Float64Array(this.cells.length);

    this.cells.forEach((cell, source) => {
      if (!cell.crust || (cell.crust.sediment || 0) <= 0.001) return;
      const visited = new Set([source]);
      const queue = [{ index: source, depth: 0 }];
      const targets = [];
      while (queue.length) {
        const current = queue.shift();
        if (current.depth >= range) continue;
        for (const neighbor of this.neighbors(current.index)) {
          if (visited.has(neighbor)) continue;
          visited.add(neighbor);
          const depth = current.depth + 1;
          queue.push({ index: neighbor, depth });
          if (!this.cells[neighbor].crust) continue;
          const drop = elevation[source] - elevation[neighbor];
          if (drop > 0.05) targets.push({ index: neighbor, weight: drop / depth });
        }
      }
      const totalWeight = targets.reduce((sum, target) => sum + target.weight, 0);
      if (!totalWeight) return;
      const available = Math.min(cell.crust.sediment * rate, Math.max(0, cell.crust.thickness - 2));
      thicknessDelta[source] -= available;
      sedimentDelta[source] -= available;
      for (const target of targets) {
        const amount = available * target.weight / totalWeight;
        thicknessDelta[target.index] += amount;
        sedimentDelta[target.index] += amount;
      }
    });

    this.cells.forEach((cell, index) => {
      if (!cell.crust) return;
      cell.crust.thickness = clamp(cell.crust.thickness + thicknessDelta[index], 2, 80);
      cell.crust.sediment = clamp((cell.crust.sediment || 0) + sedimentDelta[index], 0, cell.crust.thickness);
    });
  }

  addVolcano(q, r, strength = 1, radius = 6) {
    const existing = this.volcanoes.findIndex(v => v.q === q && v.r === r);
    if (existing >= 0) { this.volcanoes.splice(existing, 1); this.log("火山源を除去", `座標 ${q}, ${r}`); return false; }
    this.volcanoes.push({ q, r, strength, radius });
    this.log("火山源を追加", `座標 ${q}, ${r}`);
    return true;
  }

  log(title, detail) {
    this.events.unshift({ step: this.stepCount, title, detail });
    this.events = this.events.slice(0, 8);
  }

  stats() {
    const occupied = this.cells.filter(c => c.crust);
    const land = occupied.filter(c => this.surfaceElevation(c.crust) >= this.seaLevel).length;
    return { plates: this.plateCount, landPercent: Math.round(land / this.cells.length * 100), occupied: occupied.length };
  }

  landBandMetrics() {
    const isLand = this.cells.map(cell => Boolean(cell.crust && this.surfaceElevation(cell.crust) >= this.seaLevel));
    const rowFractions = Array.from({ length: this.height }, (_, r) => {
      let count = 0;
      for (let q = 0; q < this.width; q++) if (isLand[this.index(q, r)]) count++;
      return count / this.width;
    });
    const columnFractions = Array.from({ length: this.width }, (_, q) => {
      let count = 0;
      for (let r = 0; r < this.height; r++) if (isLand[this.index(q, r)]) count++;
      return count / this.height;
    });
    return { maxRowFraction: Math.max(...rowFractions), maxColumnFraction: Math.max(...columnFractions) };
  }

  serialize() {
    return JSON.stringify({ version: 1, width: this.width, height: this.height, seed: this.seed, initialSeed: this.initialSeed, stepCount: this.stepCount, seaLevel: this.seaLevel, volcanicStrength: this.volcanicStrength, volcanicRadius: this.volcanicRadius, boundaryInfluence: this.boundaryInfluence, erosionRate: this.erosionRate, erosionRange: this.erosionRange, volcanoes: this.volcanoes, cells: this.cells, events: this.events });
  }

  static deserialize(json) {
    const data = typeof json === "string" ? JSON.parse(json) : json;
    if (data.version !== 1 || !Array.isArray(data.cells) || data.cells.length !== data.width * data.height) throw new Error("対応していない惑星データです");
    const sim = new CrustSimulation(data.width, data.height, data.seed);
    Object.assign(sim, data);
    if (!Number.isFinite(sim.volcanicRadius)) sim.volcanicRadius = 1;
    if (!Number.isFinite(sim.erosionRate)) sim.erosionRate = 0.08;
    if (!Number.isFinite(sim.erosionRange)) sim.erosionRange = 2;
    sim.cells.forEach(cell => { if (cell.crust) sim.normalizeCrust(cell.crust); });
    sim.rebuildPlates();
    return sim;
  }
}

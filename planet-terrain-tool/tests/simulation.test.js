import test from "node:test";
import assert from "node:assert/strict";
import { CrustSimulation } from "../src/simulation.js";

test("hex grid wraps east-west but not north-south", () => {
  const sim = new CrustSimulation(8, 6, 42);
  assert.equal(sim.index(-1, 2), sim.index(7, 2));
  assert.equal(sim.index(8, 2), sim.index(0, 2));
  assert.equal(sim.index(2, -1), -1);
  assert.equal(sim.index(2, 6), -1);
  assert.equal(sim.neighbors(sim.index(3, 0)).length, 4);
  assert.equal(sim.neighbors(sim.index(3, 3)).length, 6);
});

test("crust attempting to cross a polar edge remains on the map", () => {
  const sim = new CrustSimulation(6, 4, 11);
  sim.volcanoes=[];
  sim.inheritanceSpread=0;
  sim.cells.forEach(cell=>cell.crust=null);
  const source=sim.index(2,0);
  sim.cells[source].crust={thickness:10,basement:-8,density:3,age:1,temperature:200,sediment:0,continentalFraction:0,velocity:{x:0,y:-1},nextDirection:5};
  sim.step();
  assert.ok(sim.cells[source].crust);
});

test("step uses a new buffer and leaves source objects untouched", () => {
  const sim = new CrustSimulation(8, 6, 7);
  const oldCells=sim.cells, oldCrust=oldCells[0].crust, oldAge=oldCrust.age;
  sim.step();
  assert.notEqual(sim.cells,oldCells);
  assert.equal(oldCrust.age,oldAge);
  assert.equal(sim.stepCount,1);
});

test("all occupied cells receive a reconstructed plate id", () => {
  const sim = new CrustSimulation(12, 8, 19);
  sim.step();
  assert.ok(sim.plateCount>0);
  assert.ok(sim.cells.filter(c=>c.crust).every(c=>c.plateId>0));
});

test("plate regions use exact 60-degree direction bins", () => {
  const sim = new CrustSimulation(8, 6, 31);
  sim.cells.forEach(cell=>cell.crust=null);
  const crustAtAngle=degrees=>({thickness:12,basement:-5,density:2.8,age:1,temperature:200,sediment:0,continentalFraction:0,velocity:{x:Math.cos(degrees*Math.PI/180),y:Math.sin(degrees*Math.PI/180)},nextDirection:0});
  const a=sim.index(3,3),b=sim.index(4,3);
  sim.cells[a].crust=crustAtAngle(29);
  sim.cells[b].crust=crustAtAngle(31);
  sim.rebuildPlates();
  assert.notEqual(sim.cells[a].plateId,sim.cells[b].plateId);
  assert.equal(sim.cells[a].plateDirection,0);
  assert.equal(sim.cells[b].plateDirection,1);
});

test("inheritance spreads to adjacent directions without duplicating crust", () => {
  const sim = new CrustSimulation(10, 8, 77);
  sim.cells.forEach(cell=>cell.crust=null);
  sim.volcanoes=[];
  sim.inheritanceSpread=1;
  const source=sim.index(4,4);
  sim.cells[source].crust={thickness:12,basement:-5,density:2.8,age:1,temperature:200,sediment:0,continentalFraction:0,velocity:{x:1,y:0},nextDirection:0};
  sim.step();
  const occupied=sim.cells.filter(cell=>cell.crust);
  assert.equal(occupied.length,1);
  assert.ok([1,5].includes(occupied[0].crust.nextDirection));
});

test("deposited thickness diffuses downhill without changing total thickness", () => {
  const sim = new CrustSimulation(8, 6, 32);
  sim.cells.forEach(cell=>cell.crust=null);
  const high=sim.index(3,3),low=sim.index(4,3);
  sim.cells[high].crust={thickness:20,basement:0,density:2.7,age:1,temperature:200,sediment:5,continentalFraction:1,velocity:{x:1,y:0},nextDirection:0};
  sim.cells[low].crust={thickness:8,basement:-10,density:2.9,age:1,temperature:200,sediment:0,continentalFraction:0,velocity:{x:1,y:0},nextDirection:0};
  sim.erosionRate=0.2;sim.erosionRange=1;
  const before=sim.cells[high].crust.thickness+sim.cells[low].crust.thickness;
  const continentalBefore=sim.cells[high].crust.thickness*sim.cells[high].crust.continentalFraction;
  sim.diffuseDeposits();
  const after=sim.cells[high].crust.thickness+sim.cells[low].crust.thickness;
  assert.ok(sim.cells[high].crust.sediment<5);
  assert.ok(sim.cells[low].crust.sediment>0);
  assert.ok(Math.abs(after-before)<1e-9);
  const continentalAfter=sim.cells[high].crust.thickness*sim.cells[high].crust.continentalFraction+sim.cells[low].crust.thickness*sim.cells[low].crust.continentalFraction;
  assert.ok(Math.abs(continentalAfter-continentalBefore)<1e-9);
});

test("grid dimensions and volcanic X-R parameters are configurable", () => {
  const sim = new CrustSimulation(52, 30, 99);
  assert.equal(sim.cells.length, 52 * 30);
  sim.volcanoes=[{q:2,r:10,strengthBias:0,radius:2}];
  sim.volcanicStrengthMean=1.4;
  sim.volcanicRadius=0.5;
  const narrow=sim.volcanicForce(8,10).heat;
  sim.volcanicRadius=2;
  const wide=sim.volcanicForce(8,10).heat;
  assert.ok(wide>narrow);
});

test("volcanic pressure uses a mean and per-source variation", () => {
  const sim = new CrustSimulation(12, 8, 101);
  sim.volcanoes=[
    {q:2,r:4,strengthBias:-1,radius:2},
    {q:8,r:4,strengthBias:1,radius:2}
  ];
  sim.volcanicStrengthMean=2;
  sim.volcanicStrengthVariation=0;
  const uniformLow=sim.volcanicForce(2,4).heat;
  const uniformHigh=sim.volcanicForce(8,4).heat;
  assert.ok(Math.abs(uniformLow-uniformHigh)<1e-9);
  sim.volcanicStrengthVariation=0.5;
  assert.ok(sim.volcanicForce(8,4).heat>sim.volcanicForce(2,4).heat);
});

test("collision thickens retained crust", () => {
  const sim = new CrustSimulation(6, 6, 2);
  const a={thickness:8,basement:-9,density:3.12,age:80,temperature:300,sediment:0,continentalFraction:0,velocity:{x:1,y:0},nextDirection:0};
  const b={thickness:31,basement:-25,density:2.7,age:500,temperature:250,sediment:0,continentalFraction:1,velocity:{x:-1,y:0},nextDirection:3};
  const result=sim.resolveCollision([a,b]);
  assert.equal(result.subducted,true);
  assert.ok(result.crust.thickness>b.thickness);
  assert.ok(result.crust.temperature>300);
});

test("serialized state round trips", () => {
  const sim = new CrustSimulation(10, 8, 123);
  sim.step(); sim.seaLevel=1.2; sim.inheritanceSpread=0.55; sim.volcanicStrengthMean=1.7; sim.volcanicStrengthVariation=0.45;
  const loaded=CrustSimulation.deserialize(sim.serialize());
  assert.equal(loaded.stepCount,1);
  assert.equal(loaded.cells.length,80);
  assert.equal(loaded.seaLevel,1.2);
  assert.equal(loaded.inheritanceSpread,0.55);
  assert.equal(loaded.volcanicStrengthMean,1.7);
  assert.equal(loaded.volcanicStrengthVariation,0.45);
  assert.equal(loaded.volcanoes.length,sim.volcanoes.length);
});

test("initial and newborn crust carry explicit continental fractions", () => {
  const sim = new CrustSimulation(38, 24, 222);
  const continental=sim.cells.filter(cell=>cell.crust?.continentalFraction===1);
  const oceanic=sim.cells.filter(cell=>cell.crust?.continentalFraction===0);
  assert.ok(continental.length>0);
  assert.ok(oceanic.length>0);
  assert.ok(continental.every(cell=>cell.crust.thickness>=30&&cell.crust.density<2.8));
  assert.ok(oceanic.every(cell=>cell.crust.thickness<14&&cell.crust.density>2.9));
  assert.equal(sim.createYoungCrust(sim.index(4,4)).continentalFraction,0);
});

test("continental interiors remain in place and do not open voids", () => {
  const sim = new CrustSimulation(8, 6, 223);
  sim.volcanoes=[];sim.inheritanceSpread=1;sim.erosionRate=0;
  for(const cell of sim.cells){cell.crust={thickness:32,basement:-10,density:2.7,age:10,temperature:200,sediment:0,continentalFraction:1,velocity:{x:1,y:0},nextDirection:0};cell.plateDirection=0}
  const center=sim.index(4,3);
  assert.equal(sim.isPlateBoundary(center),false);
  sim.step();
  assert.ok(sim.cells[center].crust);
  assert.equal(sim.cells.filter(cell=>!cell.crust).length,0);
});

test("continental boundary cells still move", () => {
  const sim = new CrustSimulation(8, 6, 224);
  sim.cells.forEach(cell=>cell.crust=null);sim.volcanoes=[];sim.inheritanceSpread=0;sim.erosionRate=0;
  const source=sim.index(3,3),destination=sim.neighborIndex(3,3,0);
  sim.cells[source].crust={thickness:32,basement:-10,density:2.7,age:10,temperature:200,sediment:0,continentalFraction:1,velocity:{x:1,y:0},nextDirection:0};
  sim.cells[source].plateDirection=0;
  assert.equal(sim.isPlateBoundary(source),true);
  sim.step();
  assert.equal(sim.cells[source].crust,null);
  assert.equal(sim.cells[destination].crust.continentalFraction,1);
});

test("continental collision compresses while oceanic crust subducts", () => {
  const sim = new CrustSimulation(8, 6, 225);
  const continental=()=>({thickness:32,basement:-10,density:2.68,age:500,temperature:250,sediment:0,continentalFraction:1,velocity:{x:1,y:0},nextDirection:0});
  const twoContinents=sim.resolveCollision([continental(),continental()]);
  assert.equal(twoContinents.subducted,false);
  assert.ok(twoContinents.crust.thickness>32);
  assert.equal(twoContinents.crust.continentalFraction,1);
  const ocean={thickness:7,basement:-9,density:3.18,age:80,temperature:300,sediment:0,continentalFraction:0,velocity:{x:-1,y:0},nextDirection:3};
  const mixed=sim.resolveCollision([ocean,continental()]);
  assert.equal(mixed.subducted,true);
  assert.equal(mixed.crust.continentalFraction,1);
});

test("continental material and area survive long runs", () => {
  const sim = new CrustSimulation(38, 24, 226);
  sim.volcanoes=[];
  const initialCells=sim.cells.filter(cell=>cell.crust?.continentalFraction>=0.5).length;
  const initialMaterial=sim.cells.reduce((sum,cell)=>sum+(cell.crust?cell.crust.thickness*cell.crust.continentalFraction:0),0);
  for(let i=0;i<100;i++)sim.step();
  const finalCells=sim.cells.filter(cell=>cell.crust?.continentalFraction>=0.5).length;
  const finalMaterial=sim.cells.reduce((sum,cell)=>sum+(cell.crust?cell.crust.thickness*cell.crust.continentalFraction:0),0);
  assert.ok(finalCells>=initialCells*0.5,`continental cells fell from ${initialCells} to ${finalCells}`);
  assert.ok(finalMaterial>=initialMaterial*0.65,`continental material fell from ${initialMaterial} to ${finalMaterial}`);
});

test("simulation remains finite across a long run", () => {
  const sim = new CrustSimulation(18, 12, 456);
  for (let i=0;i<100;i++) sim.step();
  assert.equal(sim.stepCount,100);
  assert.ok(sim.cells.some(c=>c.crust));
  for (const cell of sim.cells) {
    if (!cell.crust) continue;
    for (const value of [cell.crust.thickness,cell.crust.basement,cell.crust.density,cell.crust.age,cell.crust.temperature,cell.crust.velocity.x,cell.crust.velocity.y]) assert.ok(Number.isFinite(value));
  }
});

test("generated land does not form horizontal or vertical bands", () => {
  for (let seed=1;seed<=16;seed++) {
    const sim=new CrustSimulation(38,24,seed);
    const stats=sim.stats();
    const bands=sim.landBandMetrics();
    assert.ok(stats.landPercent>=3,`seed ${seed} should contain visible land`);
    assert.ok(bands.maxRowFraction<0.85,`seed ${seed} formed a horizontal land band`);
    assert.ok(bands.maxColumnFraction<0.85,`seed ${seed} formed a vertical land band`);
  }
});

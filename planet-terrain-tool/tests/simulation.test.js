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
  sim.cells.forEach(cell=>cell.crust=null);
  const source=sim.index(2,0);
  sim.cells[source].crust={thickness:10,basement:-8,density:3,age:1,temperature:200,velocity:{x:0,y:-1},nextDirection:5};
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

test("collision thickens retained crust", () => {
  const sim = new CrustSimulation(6, 6, 2);
  const a={thickness:8,basement:-9,density:3.12,age:80,temperature:300,velocity:{x:1,y:0},nextDirection:0};
  const b={thickness:31,basement:-25,density:2.7,age:500,temperature:250,velocity:{x:-1,y:0},nextDirection:3};
  const result=sim.resolveCollision([a,b]);
  assert.equal(result.subducted,true);
  assert.ok(result.crust.thickness>b.thickness);
  assert.ok(result.crust.temperature>300);
});

test("serialized state round trips", () => {
  const sim = new CrustSimulation(10, 8, 123);
  sim.step(); sim.seaLevel=1.2;
  const loaded=CrustSimulation.deserialize(sim.serialize());
  assert.equal(loaded.stepCount,1);
  assert.equal(loaded.cells.length,80);
  assert.equal(loaded.seaLevel,1.2);
  assert.equal(loaded.volcanoes.length,sim.volcanoes.length);
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

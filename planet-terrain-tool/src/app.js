import { CrustSimulation, DIRECTIONS } from "./simulation.js";

const $ = id => document.getElementById(id);
const canvas = $("planetCanvas");
const ctx = canvas.getContext("2d");
let sim = new CrustSimulation(38, 24);
let view = "elevation", running = false, selected = null, hovered = null;
let scale = 1, panX = 0, panY = 0, lastTime = 0, accumulator = 0;
let cellRadius = 12, originX = 0, originY = 0;
let elevationMin = -12, elevationMax = 12;

const palettes = {
  elevation: { labels:["最低","海面","最高"], colors:["#000000","#116da0","#287849","#ffffff"] },
  age: { labels:["新生","年代","古期"], colors:["#55e1cd","#d5b45d","#704663"] },
  temperature: { labels:["低温","温度","高温"], colors:["#275273","#dfb44e","#ef6544"] },
  density: { labels:["2.4","密度 g/cm³","3.5"], colors:["#85d4c7","#74878e","#453b50"] },
  plates: { labels:["運動方向による連続領域","プレート","境界は毎ステップ再構成"], colors:["#35a7a1","#dfaf59","#b96765"] }
};
const plateColors = ["#32a89c","#d19555","#865d82","#507aa2","#ae6257","#71935b","#8077b0","#b48b54","#4b9baf","#a05f77"];

function resize() {
  const box = canvas.parentElement.getBoundingClientRect();
  const dpr = Math.min(2, window.devicePixelRatio || 1);
  canvas.width = Math.floor(box.width * dpr); canvas.height = Math.floor(box.height * dpr);
  canvas.style.width = `${box.width}px`; canvas.style.height = `${box.height}px`;
  ctx.setTransform(dpr,0,0,dpr,0,0);
  const mapW = (sim.width + .5) * Math.sqrt(3), mapH = (sim.height - 1) * 1.5 + 2;
  cellRadius = Math.min((box.width - 42) / mapW, (box.height - 50) / mapH);
  originX = (box.width - mapW * cellRadius) / 2 + cellRadius * .9;
  originY = (box.height - mapH * cellRadius) / 2 + cellRadius;
  draw();
}

function center(q, r) {
  return { x: originX + (Math.sqrt(3) * (q + .5 * (r & 1))) * cellRadius, y: originY + r * 1.5 * cellRadius };
}

function hexPath(x, y, radius = cellRadius) {
  ctx.beginPath();
  for (let i=0;i<6;i++) { const angle=Math.PI/180*(60*i-30); const px=x+radius*Math.cos(angle),py=y+radius*Math.sin(angle); i?ctx.lineTo(px,py):ctx.moveTo(px,py); }
  ctx.closePath();
}

function lerpColor(a,b,t){const pa=a.match(/\w\w/g).map(x=>parseInt(x,16)),pb=b.match(/\w\w/g).map(x=>parseInt(x,16));return `rgb(${pa.map((v,i)=>Math.round(v+(pb[i]-v)*t)).join(",")})`}
function ramp(colors,t){t=Math.max(0,Math.min(.999,t));const p=t*(colors.length-1),i=Math.floor(p);return lerpColor(colors[i],colors[i+1],p-i)}

function cellColor(cell) {
  if (!cell.crust) return "#050d13";
  const c=cell.crust;
  if(view==="plates") return plateColors[(cell.plateId-1)%plateColors.length];
  if(view==="age") return ramp(palettes.age.colors,Math.min(1,c.age/1800));
  if(view==="temperature") return ramp(palettes.temperature.colors,Math.min(1,c.temperature/1200));
  if(view==="density") return ramp(palettes.density.colors,(c.density-2.4)/1.1);
  const elevation=sim.surfaceElevation(c);
  if(elevation<sim.seaLevel){const t=(elevation-elevationMin)/Math.max(.001,sim.seaLevel-elevationMin);return ramp(["#000000","#031a2b","#0d76aa"],t)}
  const t=(elevation-sim.seaLevel)/Math.max(.001,elevationMax-sim.seaLevel);
  return ramp(["#247044","#83a95e","#d8ddbd","#ffffff"],t);
}

function draw() {
  const w=canvas.clientWidth,h=canvas.clientHeight;ctx.clearRect(0,0,w,h);ctx.save();ctx.translate(panX,panY);ctx.scale(scale,scale);
  const elevations=sim.cells.filter(cell=>cell.crust).map(cell=>sim.surfaceElevation(cell.crust));
  elevationMin=elevations.length?Math.min(...elevations):sim.seaLevel;
  elevationMax=elevations.length?Math.max(...elevations):sim.seaLevel;
  sim.cells.forEach((cell,index)=>{const {q,r}=sim.coords(index),p=center(q,r);hexPath(p.x,p.y,cellRadius-.45);ctx.fillStyle=cellColor(cell);ctx.fill();ctx.strokeStyle=cell.collision?"#ff8b54":(cell.newborn?"#5cf2d8":"rgba(104,151,158,.17)");ctx.lineWidth=cell.collision?1.8:.55;ctx.stroke();});
  if(view==="plates") drawPlateBoundaries();
  if($("vectorToggle").checked) drawVectors();
  sim.volcanoes.forEach(v=>{const p=center(v.q,v.r);ctx.beginPath();ctx.arc(p.x,p.y,cellRadius*.38,0,Math.PI*2);ctx.fillStyle="rgba(255,93,49,.28)";ctx.fill();ctx.beginPath();ctx.arc(p.x,p.y,cellRadius*.14,0,Math.PI*2);ctx.fillStyle="#ff7045";ctx.shadowColor="#ff5f38";ctx.shadowBlur=9;ctx.fill();ctx.shadowBlur=0;});
  if(selected!==null){const {q,r}=sim.coords(selected),p=center(q,r);hexPath(p.x,p.y,cellRadius-.2);ctx.strokeStyle="#f1f8ef";ctx.lineWidth=1.8/scale;ctx.stroke()}
  ctx.restore();updateStats();if(view==="elevation")renderLegend();
}

function drawVectors(){sim.cells.forEach((cell,index)=>{if(!cell.crust)return;const {q,r}=sim.coords(index),p=center(q,r),v=cell.crust.velocity,len=Math.hypot(v.x,v.y)||1,size=cellRadius*.55;ctx.beginPath();ctx.moveTo(p.x-v.x/len*size*.3,p.y-v.y/len*size*.3);ctx.lineTo(p.x+v.x/len*size,p.y+v.y/len*size);ctx.strokeStyle="rgba(224,247,240,.52)";ctx.lineWidth=.7;ctx.stroke();});}
function drawPlateBoundaries(){sim.cells.forEach((cell,index)=>{if(!cell.crust)return;const {q,r}=sim.coords(index),p=center(q,r);if(sim.neighbors(index).some(i=>sim.cells[i].crust&&sim.cells[i].plateId!==cell.plateId)){hexPath(p.x,p.y,cellRadius-.4);ctx.strokeStyle="rgba(238,240,219,.42)";ctx.lineWidth=1.1;ctx.stroke();}})}

function screenToCell(clientX,clientY){const rect=canvas.getBoundingClientRect(),x=(clientX-rect.left-panX)/scale,y=(clientY-rect.top-panY)/scale;let best=null,d=Infinity;sim.cells.forEach((_,i)=>{const {q,r}=sim.coords(i),p=center(q,r),dist=Math.hypot(x-p.x,y-p.y);if(dist<d){d=dist;best=i}});return d<=cellRadius?best:null;}

function updateStats(){const stats=sim.stats();$("stepValue").textContent=String(sim.stepCount).padStart(4,"0");$("plateValue").textContent=stats.plates;$("landValue").textContent=`${stats.landPercent}%`;$("statusText").textContent=running?"RUNNING / DOUBLE BUFFER UPDATE":"READY / PAUSED";renderEvents();}
function renderLegend(){const p=palettes[view];if(view==="elevation"){$("legend").innerHTML=`<strong>${elevationMin.toFixed(1)} km</strong><span class="legend-gradient" style="background:linear-gradient(90deg,#000 0%,#0d76aa 49.5%,#247044 50.5%,#fff 100%)"></span><strong class="sea-label">海面 ${sim.seaLevel.toFixed(1)} km</strong><strong>${elevationMax.toFixed(1)} km</strong>`;return}$("legend").innerHTML=`<strong>${p.labels[0]}</strong><span class="legend-gradient" style="background:linear-gradient(90deg,${p.colors.join(",")})"></span><strong>${p.labels[2]}</strong>`;}
function renderEvents(){$("eventList").innerHTML=sim.events.map(e=>`<li><strong>${e.title}</strong><small>STEP ${String(e.step).padStart(4,"0")} · ${e.detail}</small></li>`).join("");}

function inspect(index){selected=index;const cell=sim.cells[index],{q,r}=sim.coords(index);$("emptyInspector").hidden=true;$("cellInspector").hidden=false;$("cellTitle").textContent=`CELL ${String(q).padStart(2,"0")}—${String(r).padStart(2,"0")}`;
  if(!cell.crust){$("cellSubtitle").textContent="地殻の空隙";["surfaceValue","thicknessValue","baseValue","densityValue","ageValue","temperatureValue","sedimentValue","cellPlateValue","vectorValue"].forEach(id=>$(id).textContent="—");$("directionValue").textContent="継承なし";draw();return}
  const c=cell.crust,e=sim.surfaceElevation(c),d=c.nextDirection;$("cellSubtitle").textContent=e>=sim.seaLevel?"陸域 / 地殻あり":"海域 / 地殻あり";$("surfaceValue").textContent=`${e.toFixed(2)} km`;$("thicknessValue").textContent=`${c.thickness.toFixed(1)} km`;$("baseValue").textContent=`${c.basement.toFixed(1)} km`;$("densityValue").textContent=`${c.density.toFixed(2)} g/cm³`;$("ageValue").textContent=`${Math.round(c.age)} Myr`;$("temperatureValue").textContent=`${Math.round(c.temperature)} °C`;$("sedimentValue").textContent=`${(c.sediment||0).toFixed(2)} km`;$("cellPlateValue").textContent=`P-${String(cell.plateId).padStart(2,"0")}`;$("vectorValue").textContent=`${c.velocity.x.toFixed(2)}, ${c.velocity.y.toFixed(2)}`;$("directionArrow").style.transform=`rotate(${d*60}deg)`;$("directionValue").textContent=`方向ビン ${d*60}° / ${DIRECTIONS[d].name}`;$("heightBar").style.width=`${Math.max(4,Math.min(100,(e+12)/28*100))}%`;draw();}

function step(){sim.step();if(selected!==null)inspect(selected);else draw();}
function frame(time){const dt=Math.min(100,time-lastTime);lastTime=time;if(running){accumulator+=dt;const interval=1000/Number($("speedInput").value);while(accumulator>=interval){step();accumulator-=interval}}requestAnimationFrame(frame)}
function setRunning(value){running=value;$("playButton").classList.toggle("running",running);$("playIcon").textContent=running?"Ⅱ":"▶";$("playLabel").textContent=running?"停止":"開始";draw();}

canvas.addEventListener("click",e=>{const index=screenToCell(e.clientX,e.clientY);if(index===null)return;const {q,r}=sim.coords(index);if($("volcanoTool").classList.contains("active")&&e.shiftKey){sim.addVolcano(q,r);draw()}else inspect(index)});
canvas.addEventListener("dblclick",e=>{const index=screenToCell(e.clientX,e.clientY);if(index!==null){const {q,r}=sim.coords(index);sim.addVolcano(q,r);draw()}});
canvas.addEventListener("mousemove",e=>{hovered=screenToCell(e.clientX,e.clientY);if(hovered===null){$("tooltip").hidden=true;return}const cell=sim.cells[hovered],{q,r}=sim.coords(hovered);$("tooltip").hidden=false;$("tooltip").style.left=`${e.offsetX}px`;$("tooltip").style.top=`${e.offsetY}px`;$("tooltip").textContent=cell.crust?`${q},${r} · ${sim.surfaceElevation(cell.crust).toFixed(1)} km · P-${cell.plateId}`:`${q},${r} · 地殻空隙`;});canvas.addEventListener("mouseleave",()=>$("tooltip").hidden=true);
canvas.addEventListener("wheel",e=>{e.preventDefault();scale=Math.max(.65,Math.min(2.5,scale*(e.deltaY>0?.9:1.1)));draw()},{passive:false});
$("playButton").onclick=()=>setRunning(!running);$("stepButton").onclick=()=>{setRunning(false);step()};$("resetButton").onclick=()=>{setRunning(false);sim.reset(sim.initialSeed);selected=null;draw()};
$("newWorldButton").onclick=()=>{setRunning(false);const previous=sim;sim=new CrustSimulation(Number($("gridWidthInput").value),Number($("gridHeightInput").value),Date.now());sim.seaLevel=previous.seaLevel;sim.volcanicStrength=previous.volcanicStrength;sim.volcanicRadius=previous.volcanicRadius;sim.boundaryInfluence=previous.boundaryInfluence;sim.inheritanceSpread=previous.inheritanceSpread;sim.erosionRate=previous.erosionRate;sim.erosionRange=previous.erosionRange;selected=null;resize()};
$("randomVolcanoButton").onclick=()=>{sim.addVolcano(Math.floor(Math.random()*sim.width),Math.floor(Math.random()*sim.height));draw()};$("clearVolcanoButton").onclick=()=>{sim.volcanoes=[];sim.log("火山源を除去","すべての火山源を停止");draw()};
$("speedInput").oninput=e=>$("speedOutput").textContent=`${e.target.value} steps/s`;$("gridWidthInput").oninput=e=>$("gridWidthOutput").textContent=e.target.value;$("gridHeightInput").oninput=e=>$("gridHeightOutput").textContent=e.target.value;$("seaInput").oninput=e=>{sim.seaLevel=Number(e.target.value);$("seaOutput").textContent=`${sim.seaLevel.toFixed(1)} km`;draw()};$("volcanicInput").oninput=e=>{sim.volcanicStrength=Number(e.target.value);$("volcanicOutput").textContent=`${sim.volcanicStrength.toFixed(2)}×`};$("volcanicRadiusInput").oninput=e=>{sim.volcanicRadius=Number(e.target.value);$("volcanicRadiusOutput").textContent=`${sim.volcanicRadius.toFixed(2)}×`};$("boundaryInput").oninput=e=>{sim.boundaryInfluence=Number(e.target.value);$("boundaryOutput").textContent=sim.boundaryInfluence.toFixed(2)};$("inheritanceSpreadInput").oninput=e=>{sim.inheritanceSpread=Number(e.target.value);$("inheritanceSpreadOutput").textContent=`${Math.round(sim.inheritanceSpread*100)}%`};$("erosionRateInput").oninput=e=>{sim.erosionRate=Number(e.target.value);$("erosionRateOutput").textContent=`${Math.round(sim.erosionRate*100)}%`};$("erosionRangeInput").oninput=e=>{sim.erosionRange=Number(e.target.value);$("erosionRangeOutput").textContent=`${sim.erosionRange} cells`};
document.querySelectorAll(".view-tab").forEach(button=>button.onclick=()=>{document.querySelectorAll(".view-tab").forEach(b=>b.classList.remove("active"));button.classList.add("active");view=button.dataset.view;renderLegend();draw()});$("vectorToggle").onchange=draw;
$("zoomIn").onclick=()=>{scale=Math.min(2.5,scale*1.2);draw()};$("zoomOut").onclick=()=>{scale=Math.max(.65,scale/1.2);draw()};$("zoomReset").onclick=()=>{scale=1;panX=panY=0;draw()};
$("saveButton").onclick=()=>{const blob=new Blob([sim.serialize()],{type:"application/json"}),a=document.createElement("a");a.href=URL.createObjectURL(blob);a.download=`tecton-step-${sim.stepCount}.json`;a.click();URL.revokeObjectURL(a.href)};
$("loadInput").onchange=async e=>{try{sim=CrustSimulation.deserialize(await e.target.files[0].text());selected=null;$("gridWidthInput").value=sim.width;$("gridWidthOutput").textContent=sim.width;$("gridHeightInput").value=sim.height;$("gridHeightOutput").textContent=sim.height;$("seaInput").value=sim.seaLevel;$("seaOutput").textContent=`${sim.seaLevel.toFixed(1)} km`;$("volcanicInput").value=sim.volcanicStrength;$("volcanicOutput").textContent=`${sim.volcanicStrength.toFixed(2)}×`;$("volcanicRadiusInput").value=sim.volcanicRadius;$("volcanicRadiusOutput").textContent=`${sim.volcanicRadius.toFixed(2)}×`;$("boundaryInput").value=sim.boundaryInfluence;$("boundaryOutput").textContent=sim.boundaryInfluence.toFixed(2);$("inheritanceSpreadInput").value=sim.inheritanceSpread;$("inheritanceSpreadOutput").textContent=`${Math.round(sim.inheritanceSpread*100)}%`;$("erosionRateInput").value=sim.erosionRate;$("erosionRateOutput").textContent=`${Math.round(sim.erosionRate*100)}%`;$("erosionRangeInput").value=sim.erosionRange;$("erosionRangeOutput").textContent=`${sim.erosionRange} cells`;resize()}catch(error){alert(error.message)}e.target.value=""};
window.addEventListener("resize",resize);renderLegend();resize();requestAnimationFrame(frame);

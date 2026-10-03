import test from 'node:test';
import assert from 'node:assert/strict';
import {aggregate,change,forecast,range,previousRange,series,periodStart,shiftPeriod} from '../src/lib/analytics.ts';

const entry=(date,kind,quantity,capacity,cents)=>({id:'test',date,kind,quantity,capacity,cents,revision:0});
const full={from:'2026-09-01',through:'2026-09-30',partial:false};
test('preserves repeated sessions and weights occupancy by capacity',()=>{
 const records=[entry('2026-09-01','Aula30',5,10,975),entry('2026-09-01','Aula30',5,10,975),entry('2026-09-02','Aula50',0,30,1300),entry('2026-09-02','Sala',1.25,null,875)];
 const result=aggregate(records,full);
 assert.equal(result.earnedCents,4125);assert.equal(result.records,4);assert.equal(result.classes,3);
 assert.equal(result.attendances,10);assert.equal(result.average,10/3);assert.equal(result.occupancy,20);assert.equal(result.hours,1.25);
 assert.deepEqual(result.byKind,{Sala:875,Aula30:1950,Aula50:1300});
 assert.equal(aggregate(records,full,'Aula30').classes,2);assert.equal(aggregate(records,full,'Aula50').occupancy,0);
 assert.equal(aggregate(records,full,'Sala').occupancy,null);
});
test('empty periods have zero totals but no class averages',()=>{
 const result=aggregate([],full);assert.equal(result.earnedCents,0);assert.equal(result.classes,0);assert.equal(result.average,null);assert.equal(result.occupancy,null);
});
test('includes boundary dates and excludes future or out-of-range records',()=>{
 const records=[entry('2026-08-31','Aula30',1,10,750),entry('2026-09-01','Aula30',1,10,750),entry('2026-09-30','Aula30',1,10,750),entry('2026-10-01','Aula30',1,10,750)];
 assert.equal(aggregate(records,full).earnedCents,1500);
 assert.equal(aggregate(records,range('2026-09-30','month','2026-09-01')).earnedCents,750);
});
test('partial month comparison matches elapsed day and clips shorter months',()=>{
 assert.deepEqual(previousRange('2027-03-01','month','2027-03-30'),{from:'2027-02-01',through:'2027-02-28',partial:false});
 assert.equal(previousRange('2028-03-01','month','2028-03-30').through,'2028-02-29');
 assert.equal(previousRange('2026-10-01','month','2026-10-03').through,'2026-09-03');
 assert.equal(previousRange('2026-09-01','month','2026-10-03').through,'2026-08-31');
 assert.equal(previousRange('2026-08-01','month','2026-08-03'),null);
});
test('weeks start on Monday, cross year/month boundaries and compare the same weekday',()=>{
 assert.equal(periodStart('2026-10-04','week'),'2026-09-28');assert.equal(shiftPeriod('2027-01-01','week',1),'2027-01-04');
 assert.deepEqual(previousRange('2026-10-03','week','2026-10-03'),{from:'2026-09-21',through:'2026-09-26',partial:true});
 assert.deepEqual(range('2026-08-01','week','2026-08-03'),{from:'2026-08-01',through:'2026-08-02',partial:true});
 assert.equal(previousRange('2026-08-03','week','2026-08-06'),null);
});
test('history includes empty elapsed periods without inventing pre-opening history',()=>{
 const monthly=series([], '2026-10-03','month','2026-10-03');assert.equal(monthly.length,3);assert.equal(monthly[0].key,'2026-08-01');assert.equal(monthly[2].partial,true);
 const weekly=series([], '2026-12-01','week','2026-12-01');assert.equal(weekly.length,8);assert.equal(weekly.at(-1).through,'2026-12-01');
 assert.equal(series([], '2026-08-01','week','2026-08-01')[0].from,'2026-08-01');
});
test('growth is undefined with no base; occupancy changes are absolute percentage points',()=>{
 assert.deepEqual(change(150,100),{absolute:50,percent:50});assert.deepEqual(change(0,100),{absolute:-100,percent:-100});
 assert.deepEqual(change(10,0),{absolute:10,percent:null});assert.equal(change(null,10),null);assert.equal(change(10,null),null);
 assert.equal(change(75,50).absolute,25);
});
test('forecast uses calendar days and rounds money only at the end',()=>{
 const records=[entry('2026-10-01','Sala',1,null,700),entry('2026-10-02','Aula30',5,10,975),entry('2026-10-04','Aula50',10,10,1750),entry('2026-09-30','Sala',1,null,700)];
 assert.equal(forecast(records,'2026-10-03'),Math.round(1675*31/3));assert.equal(forecast(records,'2026-10-31'),3425);
 assert.equal(forecast([], '2026-10-03'),null);
 assert.equal(forecast([entry('2028-02-01','Sala',1,null,700)],'2028-02-02'),10150);
});
test('fractional room hours do not accumulate binary rounding errors',()=>{
 const records=Array.from({length:10},()=>entry('2026-09-01','Sala',.1,null,70));assert.equal(aggregate(records,full).hours,1);
});

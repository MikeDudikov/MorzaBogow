'use strict';

// Structural lint for campaign design data. This is not a Unity or gameplay test.
const fs = require('node:fs');
const path = require('node:path');

const root = __dirname;
const dataPath = path.join(root, 'scenario-design.json');
const expectedIds = Array.from({ length: 14 }, (_, index) => 'M' + String(index + 1).padStart(2, '0'));
const knownShips = new Set(['royal', 'cutter', 'merchant', 'ram', 'crewDestroyer', 'shipDestroyer', 'boarder']);
const errors = [];
let checks = 0;

function check(condition, message) {
  checks += 1;
  if (!condition) errors.push(message);
}
function isNonNegativeInteger(value) {
  return Number.isInteger(value) && value >= 0;
}
function unique(values) {
  return Array.isArray(values) && new Set(values).size === values.length;
}

let data;
try {
  data = JSON.parse(fs.readFileSync(dataPath, 'utf8'));
} catch (error) {
  errors.push('Cannot parse scenario design JSON: ' + error.message);
  data = null;
}

if (data) {
  check(data.schemaVersion === 1, 'schemaVersion must equal 1');
  check(Array.isArray(data.scenarios), 'scenarios must be an array');

  if (Array.isArray(data.scenarios)) {
    check(data.scenarios.length === 14, 'exactly 14 campaign scenarios are required');
    check(JSON.stringify(data.scenarios.map(item => item.id)) === JSON.stringify(expectedIds),
      'scenario IDs must be ordered M01 through M14');
    check(unique(data.scenarios.map(item => item.id)), 'scenario IDs must be unique');

    for (const item of data.scenarios) {
      const id = item.id;
      check(typeof item.title === 'string' && item.title.trim().length > 0, id + ': title is missing');
      check(typeof item.arc === 'string' && item.arc.trim().length > 0, id + ': arc is missing');
      check(Array.isArray(item.targetMinutes) && item.targetMinutes.length === 2
        && item.targetMinutes.every(value => Number.isInteger(value) && value > 0)
        && item.targetMinutes[0] < item.targetMinutes[1], id + ': invalid targetMinutes range');
      check(typeof item.mapKm === 'number' && Number.isFinite(item.mapKm) && item.mapKm > 0, id + ': mapKm must be positive');
      check(typeof item.biome === 'string' && item.biome.trim().length > 0, id + ': biome is missing');
      check(item.activeHero === null || (typeof item.activeHero === 'string' && item.activeHero.trim().length > 0),
        id + ': activeHero must be null or a nonempty string');
      check(['REPAIR_AND_LIGHT_BUILD', 'REPAIR_ONLY', 'SEA_ONLY'].includes(item.baseTemplate),
        id + ': unknown baseTemplate');

      const fleet = item.initialFleet || {};
      check(Object.keys(fleet).length > 0, id + ': initialFleet must not be empty');
      let fleetTotal = 0;
      for (const [ship, count] of Object.entries(fleet)) {
        check(knownShips.has(ship), id + ': unknown ship class ' + ship);
        check(Number.isInteger(count) && count > 0, id + ': fleet count for ' + ship + ' must be positive');
        fleetTotal += Number.isInteger(count) && count > 0 ? count : 0;
      }
      check(fleetTotal > 0 && fleetTotal <= 100, id + ': initialFleet size must be between 1 and 100');

      for (const key of ['localStock', 'resourceNodes']) {
        const resources = item[key] || {};
        check(isNonNegativeInteger(resources.wood), id + ': ' + key + '.wood must be a nonnegative integer');
        check(isNonNegativeInteger(resources.silver), id + ': ' + key + '.silver must be a nonnegative integer');
      }
      if (item.missionCargo !== undefined) {
        const cargo = item.missionCargo;
        check(typeof cargo.name === 'string' && cargo.name.trim().length > 0, id + ': missionCargo.name is missing');
        check(Number.isInteger(cargo.required) && cargo.required > 0 && Number.isInteger(cargo.available) && cargo.available >= cargo.required,
          id + ': missionCargo must offer at least the required amount');
      }
      check(isNonNegativeInteger(item.enemyHullBudget), id + ': enemyHullBudget must be a nonnegative integer');
      check(Array.isArray(item.storyThreats) && item.storyThreats.length > 0, id + ': storyThreats must not be empty');

      check(Array.isArray(item.phases) && item.phases.length === 3, id + ': exactly three phases are required');
      if (Array.isArray(item.phases) && item.phases.length === 3) {
        item.phases.forEach((phase, index) => {
          check(phase.id === 'P' + (index + 1), id + ': phase IDs must be P1, P2, P3');
          check(typeof phase.label === 'string' && phase.label.trim().length > 0, id + '/' + phase.id + ': label is missing');
          check(isNonNegativeInteger(phase.planningSeconds), id + '/' + phase.id + ': planningSeconds must be nonnegative');
          check(phase.timingPolicy === 'DESIGN_TIMING_NOT_AUTOMATIC_TRANSITION',
            id + '/' + phase.id + ': timingPolicy must be explicit');
          const expectedNext = index < 2 ? 'P' + (index + 2) : null;
          check(phase.next === expectedNext, id + '/' + phase.id + ': invalid next-phase edge');
        });
      }
      check(Array.isArray(item.allWinFlags) && item.allWinFlags.length > 0 && unique(item.allWinFlags),
        id + ': allWinFlags must be nonempty and unique');
      check(Array.isArray(item.anyFailFlags) && item.anyFailFlags.length > 0 && unique(item.anyFailFlags),
        id + ': anyFailFlags must be nonempty and unique');
      if (Array.isArray(item.allWinFlags) && Array.isArray(item.anyFailFlags)) {
        check(!item.allWinFlags.some(flag => item.anyFailFlags.includes(flag)),
          id + ': win and fail flags must be disjoint');
      }
      check(item.implementationStatus === 'NOT_IMPLEMENTED', id + ': implementationStatus must not imply implemented content');
      check(item.gameplayTests === 'NOT_RUN', id + ': gameplayTests must remain NOT_RUN until actually run');
    }
  }
}

const report = {
  scope: 'Scenario design data structure lint only; not Unity implementation, gameplay, navigation, balance, or playtest validation',
  status: errors.length === 0 ? 'STRUCTURE_VALID' : 'STRUCTURE_INVALID',
  scenarioCount: data && Array.isArray(data.scenarios) ? data.scenarios.length : 0,
  structuralChecks: checks,
  gameplayImplementation: 'NOT_IMPLEMENTED',
  gameplayTests: 'NOT_RUN',
  navalPlaytests: 'NOT_RUN',
  errors,
  generatedUtc: new Date().toISOString()
};
process.stdout.write(report.status + ': ' + report.scenarioCount + ' scenarios, ' + report.structuralChecks
  + ' structural checks; gameplay remains NOT_RUN\n');
if (errors.length > 0) {
  for (const error of errors) process.stderr.write('- ' + error + '\n');
  process.exitCode = 2;
}

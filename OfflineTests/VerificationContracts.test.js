'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const checks = require('./VerificationContracts');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const route = 'ApplyRouteASingle(mission, CbRouteAPoC.Enabled);';
const executor = '_commandExecutor.Tick(mission, _elapsed, _commandProposals, _leaseGlue.HasActiveLease);';
const mission = `public override void OnMissionTick(float dt) {
    if (mission == null) { return; }
    _bench.BeginSection("routea");
    ${route}
    if (CbRouteAPoC.Control.Target != null)
        _commandExecutor.ForgetMovement(CbRouteAPoC.Control.Target.Identity as Formation);
    _bench.EndSection("routea"); _bench.BeginSection("executor");
    ${executor}
}`;
test('mission accepts the supported source and braced decompiler shapes', () => {
    checks.checkMissionTick(mission);
    checks.checkMissionTick(mission.replace('_commandExecutor.ForgetMovement(CbRouteAPoC.Control.Target.Identity as Formation);',
        '{ _commandExecutor.ForgetMovement(CbRouteAPoC.Control.Target.Identity as Formation); }'));
});
for (const [name, code] of [
    ['missing reservation', mission.replace(route, '')],
    ['missing executor', mission.replace(executor, '')],
    ['reversed calls', mission.replace(route, executor).replace(executor + '\n}', route + '\n}')],
    ['wrong inline branch', mission.replace(route, 'if (!CbRouteAPoC.Enabled) ' + route)],
    ['wrong braced branch', mission.replace(route, 'if (!CbRouteAPoC.Enabled) { ' + route + ' }')],
    ['early return between calls', mission.replace(route, route + ' if (CbRouteAPoC.Enabled) return;')],
    ['duplicate reservation', mission.replace(route, route + route)]
]) test('mission rejects ' + name, () => assert.throws(() => checks.checkMissionTick(code)));

const candidate = 'protected override void CalculateCurrentOrder() { CurrentOrder = MovementOrder.MovementOrderStop; IsCurrentOrderChanged = false; }';
test('candidate accepts the prepared order and final false assignment', () => checks.checkCandidate(candidate, 'CbHoldBehavior'));
for (const [name, code] of [
    ['final true assignment', candidate.replace('false;', 'false; IsCurrentOrderChanged = true;')],
    ['conditional false assignment', candidate.replace('IsCurrentOrderChanged = false;', 'if (enabled) IsCurrentOrderChanged = false;')],
    ['helper side effect', candidate.replace('CurrentOrder =', 'WriteThroughHelper(); CurrentOrder =')],
    ['wrong prepared order', candidate.replace('MovementOrderStop', 'MovementOrderAdvance')],
    ['comment-only false assignment', candidate.replace('IsCurrentOrderChanged = false;', '// IsCurrentOrderChanged = false;\n')]
]) test('candidate rejects ' + name, () => assert.throws(() => checks.checkCandidate(code, 'CbHoldBehavior')));

const mcm = `public class CombatBrainMcmSettings {
    private static readonly string[] Cps3ModeNames = { "A: Native", "B: Shadow", "C: Coexist", "D: Exclusive", "E: Route A" };
    private readonly Dropdown<string> _cps3ModeDropdown = new Dropdown<string>(Cps3ModeNames, 0);
    public bool Cps3ValidationEnabled { get => Cps3RuntimeSettings.Enabled; set => Cps3RuntimeSettings.Enabled = value; }
    public bool Cps3ObserverEnabled { get => Cps3RuntimeSettings.ObserverEnabled; set => Cps3RuntimeSettings.ObserverEnabled = value; }
    public bool RouteAMovementEnabled { get => Cps2PoCSettings.RouteAEnabled; set => Cps2PoCSettings.RouteAEnabled = value; }
    public bool Cps3ShadowLogEnabled { get => Cps3RuntimeSettings.ShadowWouldApplyLogEnabled; set => Cps3RuntimeSettings.ShadowWouldApplyLogEnabled = value; }
    public Dropdown<string> Cps3ValidationMode {
        get {
            if (Cps3RuntimeSettings.NeedsModeSync(_cps3ModeDropdown.SelectedIndex)) {
                Cps3RuntimeSettings.Mode = Cps3RuntimeSettings.ModeFromSelectedIndex(_cps3ModeDropdown.SelectedIndex);
            }
            return _cps3ModeDropdown;
        }
        set {
            Cps3RuntimeSettings.Mode = Cps3RuntimeSettings.ModeFromSelectedIndex(value.SelectedIndex);
            _cps3ModeDropdown.SelectedIndex = value.SelectedIndex;
        }
    }
}`;
test('MCM accepts expression accessors and block accessors from decompilation', () => {
    checks.checkMcm(mcm);
    checks.checkMcm(mcm.replace(/get => ([^;]+);/g, 'get { return $1; }')
        .replace(/set => ([^;]+);/g, 'set { $1; }')
        .replace('= { "A:', '= new string[5] { "A:'));
});
for (const [name, code] of [
    ['constant observer setter', mcm.replace('ObserverEnabled = value;', 'ObserverEnabled = false;')],
    ['wrong observer field', mcm.replace('get => Cps3RuntimeSettings.ObserverEnabled;', 'get => Cps3RuntimeSettings.Enabled;')],
    ['missing E option', mcm.replace(', "E: Route A"', '')],
    ['disconnected dropdown', mcm.replace('(Cps3ModeNames, 0)', '(OtherNames, 0)')],
    ['wrong initial index', mcm.replace('(Cps3ModeNames, 0)', '(Cps3ModeNames, 1)')],
    ['missing instance sync', mcm.replace('Cps3RuntimeSettings.Mode = Cps3RuntimeSettings.ModeFromSelectedIndex(_cps3ModeDropdown.SelectedIndex);', '')],
    ['missing setter forwarding', mcm.replace('Cps3RuntimeSettings.Mode = Cps3RuntimeSettings.ModeFromSelectedIndex(value.SelectedIndex);', '')]
]) test('MCM rejects ' + name, () => assert.throws(() => checks.checkMcm(code)));

const groups = ['TestOne', 'TestTwo'];
const complete = '[GROUP PASS] TestOne checks=1\n[GROUP PASS] TestTwo checks=12\nALL PASS\n';
test('ledger accepts complete groups without locking an assertion total', () => checks.checkRegressionLog(complete, groups));
for (const [name, log] of [
    ['missing group', '[GROUP PASS] TestOne checks=1394\nALL PASS\n'],
    ['no groups despite many PASS lines', '[PASS] other check\n'.repeat(1394) + 'ALL PASS\n'],
    ['empty group', complete.replace('checks=1\n', 'checks=0\n')],
    ['duplicate group', complete + '[GROUP PASS] TestOne checks=1\n'],
    ['failed group', complete.replace('GROUP PASS', 'GROUP FAIL')],
    ['unknown group', complete + '[GROUP PASS] TestOther checks=1\n']
]) test('ledger rejects ' + name, () => assert.throws(() => checks.checkRegressionLog(log, groups)));

const registration = `private static int Main() { RunTest(TestOne); return 0; }
    private static void TestOne() { Check(true, "fixture only"); RunTest(TestTwo); }
    private static void TestTwo() { Check(true, "fixture only"); }`;
test('registration includes nested test groups', () => checks.checkTestRegistration([registration], groups));
test('registration rejects an omitted nested call', () => assert.throws(() =>
    checks.checkTestRegistration([registration.replace('RunTest(TestTwo);', '')], groups)));
test('registration rejects a removed test definition', () => assert.throws(() =>
    checks.checkTestRegistration([registration.replace('private static void TestTwo() { Check(true, "fixture only"); }', '')], groups)));
test('registration rejects an undeclared manifest entry', () => assert.throws(() =>
    checks.checkTestRegistration([registration], [...groups, 'TestMissing'])));
test('literal and comment braces cannot corrupt extraction', () => {
    const code = 'void Example() { var text = "}"; /* { */ char brace = \'{\'; DoWork(); }';
    assert.equal(checks.readBody(code, /\bExample\(\)/), code.slice(code.indexOf('{')));
});
test('CLI reads real PowerShell UTF-16LE and UTF-8 log encodings', () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'CombatBrain-ledger-'));
    try {
        const manifest = JSON.parse(fs.readFileSync(path.join(__dirname, 'RequiredTestGroups.json'), 'utf8'));
        const log = manifest.RequiredGroups.map(name => '[GROUP PASS] ' + name + ' checks=1').join('\r\n');
        for (const encoding of ['utf8', 'utf16le']) {
            const file = path.join(dir, encoding + '.log');
            fs.writeFileSync(file, '\uFEFF' + log, encoding);
            const run = spawnSync(process.execPath, [path.join(__dirname, 'VerificationContracts.js'), '--regression-log', file], { encoding: 'utf8' });
            assert.equal(run.status, 0, run.stderr);
        }
    } finally { fs.rmSync(dir, { recursive: true, force: true }); }
});

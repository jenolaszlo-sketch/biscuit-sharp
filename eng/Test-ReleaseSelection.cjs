const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const workflow = fs.readFileSync(path.join(__dirname, '../.github/workflows/publish.yml'), 'utf8');
const block = workflow.match(/          script: \|\r?\n((?:            .*(?:\r?\n|$))*)/);
assert(block, 'Publisher release resolver was not found');
const body = block[1].split(/\r?\n/).map(line => line.slice(12)).join('\n');
const AsyncFunction = Object.getPrototypeOf(async function () {}).constructor;
const resolve = new AsyncFunction('github', 'context', 'core', 'process', body);
const current = 'a'.repeat(40);
const older = 'b'.repeat(40);
const run = (id, sha = current, overrides = {}) => ({
    id, run_number: id, head_sha: sha, head_branch: 'main',
    event: 'push', status: 'completed', conclusion: 'success', ...overrides
});

async function select({ id = '', sha = '', runs = [], lookup = runs, dispatch = current } = {}) {
    const outputs = {};
    const calls = [];
    const github = {
        request: async (route, request) => {
            assert.equal(route, 'GET /repos/{owner}/{repo}/actions/runs/{run_id}');
            calls.push({ get: request });
            const found = lookup.find(item => item.id === request.run_id);
            if (!found) throw new Error('Run not found');
            return { data: found };
        },
        paginate: async (route, request) => {
            // Passing an undefined SDK method otherwise queries the API root.
            assert.equal(route, 'GET /repos/{owner}/{repo}/actions/workflows/{workflow_id}/runs');
            calls.push({ list: request });
            return runs;
        }
    };
    await resolve(github, { repo: { owner: 'owner', repo: 'repo' }, sha: dispatch },
        { setOutput: (name, value) => { outputs[name] = value; } },
        { env: { REQUESTED_RUN_ID: id, REQUESTED_SHA: sha } });
    return { outputs, calls };
}

module.exports = (async () => {
    // Never fall back to a newer successful run for a different commit.
    const result = await select({ runs: [
        run(50, older), run(4), run(8), run(60, current, { conclusion: 'failure' }),
        run(61, current, { status: 'in_progress' }),
        run(62, current, { head_branch: 'feature' }),
        run(63, current, { event: 'pull_request' })
    ] });
    assert.deepEqual(result.outputs, { ci_run_id: '8', release_sha: current });
    assert.equal(result.calls[0].list.head_sha, current);
    assert.equal(result.calls[0].list.workflow_id, 'ci.yml');
    assert.equal(result.calls[0].list.branch, 'main');
    assert.equal(result.calls[0].list.event, 'push');
    assert.equal(result.calls[0].list.status, 'success');

    await assert.rejects(select({ runs: [run(99, older)] }), /No successful CI run/);
    await assert.rejects(select({ runs: [run(99, current, { conclusion: 'failure' })] }), /No successful CI run/);
    await assert.rejects(select({ runs: [run(1)], lookup: [run(1, older)] }), /SHA differs/);
    const historical = await select({ id: '50', lookup: [run(50, older)] });
    assert.deepEqual(historical.outputs, { ci_run_id: '50', release_sha: older });
    assert.equal(historical.calls.filter(call => call.list).length, 0);
    assert.equal((await select({ sha: older, runs: [run(50, older)] })).outputs.release_sha, older);
    await assert.rejects(select({ id: '50', sha: current, lookup: [run(50, older)] }), /SHA differs/);
    for (const id of ['abc', '0', '-1', '999999999999999999999'])
        await assert.rejects(select({ id }), /positive numeric/);
    await assert.rejects(select({ sha: 'short' }), /full lowercase commit SHA/);

    // Preserve the validated output as the only source for artifact download.
    assert.match(workflow, /Download immutable package artifact[\s\S]*?RUN_ID: \$\{\{ steps\.release\.outputs\.ci_run_id \}\}/);
    assert.match(workflow, /test "\$job_count" -eq 19/);
    assert.match(workflow, /environment: nuget-production/);
    assert.equal((workflow.match(/required: false/g) || []).length, 2);
    console.log('Release selection passed: exact-commit discovery, historical overrides, fail-closed rejection, validated artifact binding.');
})();

// Exercise the routes actually used by the resolver with the action's real SDK.
// Query this CI run's own SHA, so no historical run or artifact is required.
module.exports.checkSdk = async (github, context) => {
    const getRoute = body.match(/github\.request\('([^']+)'/)[1];
    const listRoute = body.match(/github\.paginate\('([^']+)'/)[1];
    const { owner, repo } = context.repo;
    const { data: thisRun } = await github.request(getRoute, { owner, repo, run_id: context.runId });
    const runs = await github.paginate(listRoute, {
        owner, repo, workflow_id: thisRun.workflow_id, head_sha: thisRun.head_sha, per_page: 100
    });
    assert(runs.some(item => item.id === context.runId), 'Real SDK discovery did not return this CI run');
    console.log('Real github-script SDK routes found this CI run: ' + context.runId);
};

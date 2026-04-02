#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

CONFIG="${CONFIG:-Release}"
TIMEOUT="${TIMEOUT:-120}"

# Colors (disabled if not a terminal)
if [ -t 1 ]; then
    RED='\033[0;31m'
    GREEN='\033[0;32m'
    YELLOW='\033[0;33m'
    CYAN='\033[0;36m'
    NC='\033[0m'
else
    RED='' GREEN='' YELLOW='' CYAN='' NC=''
fi

print_header() { echo -e "\n${CYAN}=== $1 ===${NC}\n"; }
print_pass()   { echo -e "${GREEN}PASS: $1${NC}"; }
print_fail()   { echo -e "${RED}FAIL: $1${NC}"; }
print_warn()   { echo -e "${YELLOW}WARNING: $1${NC}"; }

usage() {
    cat <<EOF
Usage: ./test.sh <command>

Commands:
  unit                Run unit tests (Hermes.Tests)
  integration         Run integration tests (IntegrationTestApp)
  single-instance     Run single-instance integration tests
  all                 Run all test suites

Options (via environment):
  CONFIG=Debug|Release    Build configuration (default: Release)
  TIMEOUT=120             Timeout in seconds for integration tests (default: 120)

Examples:
  ./test.sh unit
  ./test.sh integration
  ./test.sh all
  CONFIG=Debug ./test.sh integration
EOF
    exit 1
}

# Verify markers in integration test output.
# Expects a file path as the argument.
check_markers() {
    local output_file="$1"
    local suite_name="${2:-Integration}"
    local failed=0

    if ! grep -q "HERMES_READY:" "$output_file"; then
        print_fail "Never received HERMES_READY marker"
        failed=1
    fi

    if grep -q "HERMES_TEST_FAIL:" "$output_file"; then
        print_fail "One or more ${suite_name} tests failed"
        failed=1
    fi

    if ! grep -q "HERMES_TEST_SUMMARY:" "$output_file"; then
        print_fail "${suite_name} tests did not complete (no HERMES_TEST_SUMMARY marker)"
        failed=1
    fi

    return $failed
}

run_unit() {
    print_header "Unit Tests"
    dotnet test tests/Hermes.Tests -c "$CONFIG" --verbosity normal
    print_pass "Unit tests completed"
}

run_integration() {
    print_header "Integration Tests (IntegrationTestApp)"

    # Build native library (macOS)
    if [[ "$(uname)" == "Darwin" ]]; then
        print_header "Building native macOS library"
        make -C src/Hermes.Native.macOS universal
    fi

    dotnet build samples/IntegrationTestApp -c "$CONFIG"

    local app_path="samples/IntegrationTestApp/bin/${CONFIG}/net10.0/IntegrationTestApp"
    if [ ! -f "$app_path" ]; then
        print_fail "App not found at $app_path"
        return 1
    fi
    chmod +x "$app_path"

    local output_file
    output_file="$(mktemp)"
    trap "rm -f '$output_file'" RETURN

    export HERMES_INTEGRATION_TEST=1
    export HERMES_INTEGRATION_TEST_EXIT=1
    export HERMES_SMOKE_TEST=1
    export HERMES_SMOKE_TEST_EXIT=1

    echo "Running: $app_path (timeout: ${TIMEOUT}s)"

    set +e
    "$app_path" > "$output_file" 2>&1 &
    local pid=$!

    local elapsed=0
    while kill -0 "$pid" 2>/dev/null; do
        if [[ $elapsed -ge $TIMEOUT ]]; then
            kill "$pid" 2>/dev/null || true
            wait "$pid" 2>/dev/null || true
            echo ""
            echo "=== Application Output ==="
            cat "$output_file"
            print_fail "Process did not exit within ${TIMEOUT}s"
            return 1
        fi
        sleep 1
        ((elapsed++))
    done

    wait "$pid"
    local exit_code=$?
    set -e

    echo ""
    echo "=== Application Output ==="
    cat "$output_file"
    echo ""

    if [[ $exit_code -ne 0 ]]; then
        print_fail "Process exited with code $exit_code"
        return 1
    fi

    check_markers "$output_file" "Integration"
    print_pass "Integration tests completed"
}

run_single_instance() {
    print_header "Single Instance Integration Tests"

    dotnet build tests/Hermes.SingleInstance.IntegrationTests -c "$CONFIG"

    local app_path="tests/Hermes.SingleInstance.IntegrationTests/bin/${CONFIG}/net10.0/Hermes.SingleInstance.IntegrationTests"
    if [ ! -f "$app_path" ]; then
        print_fail "App not found at $app_path"
        return 1
    fi
    chmod +x "$app_path"

    local output_file
    output_file="$(mktemp)"
    trap "rm -f '$output_file'" RETURN

    echo "Running: $app_path (timeout: ${TIMEOUT}s)"

    set +e
    "$app_path" > "$output_file" 2>&1 &
    local pid=$!

    local elapsed=0
    while kill -0 "$pid" 2>/dev/null; do
        if [[ $elapsed -ge $TIMEOUT ]]; then
            kill "$pid" 2>/dev/null || true
            wait "$pid" 2>/dev/null || true
            echo ""
            echo "=== Test Output ==="
            cat "$output_file"
            print_fail "Process did not exit within ${TIMEOUT}s"
            return 1
        fi
        sleep 1
        ((elapsed++))
    done

    wait "$pid"
    local exit_code=$?
    set -e

    echo ""
    echo "=== Test Output ==="
    cat "$output_file"
    echo ""

    check_markers "$output_file" "Single Instance"
    print_pass "Single instance integration tests completed"
}

run_all() {
    local failed=0

    run_unit || failed=1
    run_integration || failed=1
    run_single_instance || failed=1

    echo ""
    if [[ $failed -ne 0 ]]; then
        print_fail "One or more test suites failed"
        return 1
    fi
    print_pass "All test suites passed"
}

# --- Main ---

if [[ $# -lt 1 ]]; then
    usage
fi

case "$1" in
    unit)             run_unit ;;
    integration)      run_integration ;;
    single-instance)  run_single_instance ;;
    all)              run_all ;;
    -h|--help|help)   usage ;;
    *)
        echo "Unknown command: $1"
        usage
        ;;
esac

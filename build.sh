#!/usr/bin/env bash

# Selena Build Script for Unix/Linux/macOS
set -e

# Default values
CONFIGURATION="Release"
VERSION="1.0.0"
RUN_TESTS=false
RUN_PACK=false
RUN_BENCHMARK=false
RUN_CLEAN=false

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --configuration|-c)
            CONFIGURATION="$2"
            shift 2
            ;;
        --version|-v)
            VERSION="$2"
            shift 2
            ;;
        --test|-t)
            RUN_TESTS=true
            shift
            ;;
        --pack|-p)
            RUN_PACK=true
            shift
            ;;
        --benchmark|-b)
            RUN_BENCHMARK=true
            shift
            ;;
        --clean)
            RUN_CLEAN=true
            shift
            ;;
        --help|-h)
            echo "Usage: ./build.sh [options]"
            echo "Options:"
            echo "  -c, --configuration  Build configuration (Debug/Release) [default: Release]"
            echo "  -v, --version        Version number [default: 1.0.0]"
            echo "  -t, --test           Run tests"
            echo "  -p, --pack           Create NuGet package"
            echo "  -b, --benchmark      Run benchmarks"
            echo "      --clean          Clean build artifacts"
            echo "  -h, --help           Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

function print_header {
    echo ""
    echo -e "${CYAN}========================================"
    echo -e " $1"
    echo -e "========================================${NC}"
    echo ""
}

# Clean
if [ "$RUN_CLEAN" = true ]; then
    print_header "Cleaning Solution"
    dotnet clean --configuration $CONFIGURATION
    if [ -d "artifacts" ]; then
        rm -rf artifacts
    fi
    echo -e "${GREEN}Clean completed!${NC}"
fi

# Build
print_header "Building Selena"
echo -e "${YELLOW}Configuration: $CONFIGURATION${NC}"
echo -e "${YELLOW}Version: $VERSION${NC}"

dotnet build --configuration $CONFIGURATION -p:Version=$VERSION
if [ $? -ne 0 ]; then
    echo -e "${RED}Build failed!${NC}"
    exit 1
fi
echo -e "${GREEN}Build completed successfully!${NC}"

# Test
if [ "$RUN_TESTS" = true ]; then
    print_header "Running Tests"
    dotnet test --configuration $CONFIGURATION --no-build \
        --logger "console;verbosity=normal" \
        --collect:"XPlat Code Coverage" \
        --results-directory "./artifacts/TestResults"
    
    if [ $? -ne 0 ]; then
        echo -e "${RED}Tests failed!${NC}"
        exit 1
    fi
    echo -e "${GREEN}All tests passed!${NC}"
fi

# Benchmark
if [ "$RUN_BENCHMARK" = true ]; then
    print_header "Running Benchmarks"
    pushd tests/Selena.Benchmarks > /dev/null
    dotnet run --configuration Release
    if [ $? -ne 0 ]; then
        echo -e "${RED}Benchmarks failed!${NC}"
        exit 1
    fi
    popd > /dev/null
    echo -e "${GREEN}Benchmarks completed!${NC}"
fi

# Pack
if [ "$RUN_PACK" = true ]; then
    print_header "Creating NuGet Package"
    dotnet pack src/Selena/Selena.csproj \
        --configuration $CONFIGURATION \
        --no-build \
        -p:Version=$VERSION \
        --output "./artifacts/packages"
    
    if [ $? -ne 0 ]; then
        echo -e "${RED}Pack failed!${NC}"
        exit 1
    fi
    
    PACKAGE_FILE=$(ls artifacts/packages/*.nupkg | head -n 1)
    PACKAGE_SIZE=$(du -h "$PACKAGE_FILE" | cut -f1)
    echo -e "${GREEN}Package created: $(basename $PACKAGE_FILE)${NC}"
    echo -e "${YELLOW}Package size: $PACKAGE_SIZE${NC}"
fi

print_header "Build Complete!"
echo -e "${YELLOW}Next steps:${NC}"
echo "  - Run examples: dotnet run --project examples/Selena.Examples"
echo "  - Run tests: ./build.sh --test"
echo "  - Create package: ./build.sh --pack"
echo "  - Run benchmarks: ./build.sh --benchmark"

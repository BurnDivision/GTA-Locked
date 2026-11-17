// T6 (Codex contribution, integrated by Claude): unit tests for the traffic gap/yield decision.
// Build: g++ -std=c++11 -Wall -Wextra -I<carnage3d/src> traffic_gap_test.cpp
#include "TrafficGap.h"
#include <iostream>

int main()
{
    using TrafficGap::Decision;
    struct Case {
        const char* name;
        float along, side, distance, otherAlong;
        bool priority, ignore;
        Decision expected;
    };
    // Chassis half-length sum = 4m, gap margin = 1m, map unit = 2m.
    // These catch a blanket override, missing slowdown, and crossing-rule regressions.
    const Case cases[] = {
        {"stopped leader during override", 4.5f, 0, 4.5f, 0, false, true, Decision::Stop},
        {"overlap during override", 1, 0, 1, 0, false, true, Decision::Stop},
        {"leader during ordinary wait", 4.5f, 0, 4.5f, 0, false, false, Decision::Stop},
        {"approaching leader during override", 6, 0, 6, 0, false, true, Decision::Slow},
        {"clear leader", 7, 0, 7, 0, false, true, Decision::Clear},
        {"exact stop margin slows", 5, 0, 5, 0, false, true, Decision::Slow},
        {"lateral boundary remains protected", 4, 1.2f, 4.18f, 0, false, true, Decision::Stop},
        {"crossing yields normally", 4, 2, 4.48f, 0, false, false, Decision::Stop},
        {"crossing override breaks wait", 4, 2, 4.48f, 0, false, true, Decision::Clear},
        {"crossing priority", 4, 2, 4.48f, 4, true, false, Decision::Clear},
        {"crossing other priority", 4, 2, 4.48f, 4, false, false, Decision::Stop},
        {"outside forward cone", 2, 4, 4.48f, 0, false, false, Decision::Clear},
        {"distant crossing", 8, 2, 8.25f, 0, false, false, Decision::Clear},
        {"behind", -1, 0, 1, 0, false, false, Decision::Clear},
        {"abreast", 0, 0, 0, 0, false, true, Decision::Clear},
        {"ordinary slowdown", 6, 0, 6, 0, false, false, Decision::Slow},
    };
    int failures = 0;
    for (const auto& c : cases) {
        if (TrafficGap::Evaluate(c.along, c.side, c.distance, 4, 1, 2,
                c.otherAlong, c.priority, c.ignore) != c.expected) {
            std::cerr << "FAIL: " << c.name << '\n';
            ++failures;
        }
    }
    std::cout << sizeof(cases) / sizeof(cases[0]) << " cases, " << failures << " failures\n";
    return failures ? 1 : 0;
}

local PositionalParametersData = {}

-- Relative Position data
PositionalParametersData.default_relative_distance = {
    min = 70.0,
    max = 490.0
}

PositionalParametersData.character_relative_distance_offsets = {}

-- minimum_distance_offset = added padding in front of the character increasing the minimum relative distance (used for the minimum and maximum settings)
-- position_vs_relative_distance_offset = added offset which results in the relative_vs distance of the characters to be different from the distance in x positions, likely due to the x position being shifted behind the
-- position used to calculate the relative_vs distance.

-- honda
PositionalParametersData.character_relative_distance_offsets[20] = {
    minimum_distance_offset = 5.0,
    position_vs_relative_distance_offset = 5.0
}
-- blanka
PositionalParametersData.character_relative_distance_offsets[15] = {
    minimum_distance_offset = 5.0,
    position_vs_relative_distance_offset = 5.0
}
-- sagat
PositionalParametersData.character_relative_distance_offsets[25] = {
    minimum_distance_offset = 5.0,
    position_vs_relative_distance_offset = 0
}
-- marisa
PositionalParametersData.character_relative_distance_offsets[17] = {
    minimum_distance_offset = 5.0,
    position_vs_relative_distance_offset = 0
}
-- gief
PositionalParametersData.character_relative_distance_offsets[6] = {
    minimum_distance_offset = 8.0,
    position_vs_relative_distance_offset = 8.0
}
-- alex
PositionalParametersData.character_relative_distance_offsets[31] = {
    minimum_distance_offset = 5.0,
    position_vs_relative_distance_offset = 0
}

--[[
    Characters default min/max relative distance values
    
    Special characters offsets from these values

    Gief = 8
    Blanka/Sagat/Marisa/Honda = 5
]]
-- absolute position data

PositionalParametersData.default_screen_position = {
    min = -765.0,
    max = 765.0
}

PositionalParametersData.preset_relative_distance_offsets = {
    values = {
        0.0,
        40.0,
        100.0,
        140.0,
        212.0,
        230.0,
        330.0
    },
    names = {
        "Point Blank",
        "Close Range",
        "Mid Range",
        "Far Range",
        "Throw Tech Distance",
        "Roundstart Distance",
        "Zoning Range",
        "Max Range"
    }
}

return PositionalParametersData

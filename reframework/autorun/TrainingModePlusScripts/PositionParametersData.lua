PositionalParametersData = {}

-- Relative Position data
PositionalParametersData.default_relative_distance = {
    min = 70.0,
    max = 490.0,
}

PositionalParametersData.character_relative_distance_offsets = {}

-- honda
PositionalParametersData.character_relative_distance_offsets[20] = 5.0
-- blanka
PositionalParametersData.character_relative_distance_offsets[15] = 5.0
-- sagat
PositionalParametersData.character_relative_distance_offsets[25] = 5.0
-- marisa
PositionalParametersData.character_relative_distance_offsets[17] = 5.0
-- gief
PositionalParametersData.character_relative_distance_offsets[6] = 8.0

--[[
    Characters default min/max relative distance values
    
    Special characters offsets from these values

    Gief = 10
    Blanka/Sagat/Marisa/Honda = 5
]]

-- absolute position data

PositionalParametersData.default_screen_position = {
    min = -765.0,
    max = 765.0,
}

return PositionalParametersData
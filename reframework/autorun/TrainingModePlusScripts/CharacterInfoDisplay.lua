-- intellinsense
local re = re
local sdk = sdk
local reframework = reframework
local imgui = imgui

local module = {}

module.name = "Character Info Display"
module.description =
    "Module for displaying detailed live character information on screen, such as gauges, timers, and positions."

module.data = {}
module.ui = {}

module.data.UniqueCharData = require("TrainingModePlusScripts/UniqueCharacterParametersData")

function module.init()
    module.data.TrainingManager = sdk.get_managed_singleton("app.training.TrainingManager")
    module.data.TrainingData = module.data.TrainingManager:get_field("_tData")
    module.data.SelectMenu = module.data.TrainingData:get_field("SelectMenu")

    local gBattle = sdk.find_type_definition("gBattle")
    local sPlayer = gBattle:get_field("Player"):get_data(nil)
    local sTeam = gBattle:get_field("Team"):get_data(nil)
    local cPlayer = sPlayer.mcPlayer
    local cTeam = sTeam.mcTeam
    -- use sGame.stage_timer == 1 to check for the refresh (you can apply all the settings you want here and they won't get overwritten by the game at this point)
    module.data.sGame = gBattle:get_field("Game"):get_data(nil)
    module.data.live_P1 = cPlayer[0]
    module.data.live_P2 = cPlayer[1]
    module.data.team_P1 = cTeam[0]
    module.data.team_P2 = cTeam[1]
end

function module.on_frame()
    -- nothing to do per frame
end

function module.draw_ui()
    local p1 = module.data.live_P1
    local p2 = module.data.live_P2
    local t1 = module.data.team_P1
    local t2 = module.data.team_P2

    local char_id1 = module.data.SelectMenu.PlayerDatas[0].FighterID
    local char_id2 = module.data.SelectMenu.PlayerDatas[1].FighterID
    local char_datas
    if char_id1 == char_id2 then
        char_datas = {module.data.UniqueCharData[char_id1]}
    else
        char_datas = {module.data.UniqueCharData[char_id1], module.data.UniqueCharData[char_id2]}
    end

    -- nothing to draw for now
    if imgui.collapsing_header("Character Info Display") then
        -- display current health, drive and super for each character

        -- health
        local heal_new1 = p1.heal_new
        local heal_new2 = p2.heal_new
        local vital_new1 = p1.vital_new
        local vital_new2 = p2.vital_new
        local vital_max1 = p1.vital_max
        local vital_max2 = p2.vital_max

        imgui.text("P1 Health: " .. tostring(vital_new1) .. " / " .. tostring(vital_max1))
        imgui.text("P2 Health: " .. tostring(vital_new2) .. " / " .. tostring(vital_max2))

        if heal_new1 ~= vital_new1 then
            imgui.text("P1 Gray Health: " .. tostring(heal_new1))
        end

        if heal_new2 ~= vital_new2 then
            imgui.text("P2 Gray Health: " .. tostring(heal_new2))
        end

        imgui.separator()

        -- drive
        local drive_new1 = p1.focus_new
        local drive_new2 = p2.focus_new

        imgui.text("P1 Drive: " .. tostring(drive_new1))
        imgui.text("P2 Drive: " .. tostring(drive_new2))

        imgui.separator()

        -- super
        local super_gauge1 = t1.mSuperGauge
        local super_gauge2 = t2.mSuperGauge

        imgui.text("P1 Super Gauge: " .. tostring(super_gauge1))
        imgui.text("P2 Super Gauge: " .. tostring(super_gauge2))

        imgui.separator()

        -- unique character data (only install timer as the rest are visible through the ingame UI)

        for index, char_data in pairs(char_datas) do
            if char_data then
                if char_data.timers then
                    for _, timerData in pairs(char_data.timers) do
                        if timerData.install == true then
                            -- print timer value with character and install name

                            if index == 1 then
                                imgui.text(
                                    char_data.name ..
                                        " - " ..
                                            timerData.name ..
                                                ": " .. p1.style_timer .. " / " .. tostring(timerData.timerMaxValue)
                                )
                            else
                                imgui.text(
                                    char_data.name ..
                                        " - " ..
                                            timerData.name ..
                                                ": " .. p2.style_timer .. " / " .. tostring(timerData.timerMaxValue)
                                )
                            end
                        end
                    end
                end
            end
        end

        if char_datas[0] ~= nil or char_datas[1] ~= nil then
            imgui.separator()
        end

        -- player position
        local pos1 = p1.pos.x
        local pos2 = p2.pos.x
        local distance = p1.vs_distance

        imgui.text("P1 Position: " .. string.format("%.2f", pos1:ToFloat()))
        imgui.text("P2 Position: " .. string.format("%.2f", pos2:ToFloat()))
        imgui.text("Distance Between P1 and P2: " .. string.format("%.2f", distance:ToFloat()))
    end
end

return module

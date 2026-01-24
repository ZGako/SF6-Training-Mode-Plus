-- intellinsense
local re = re
local sdk = sdk
local reframework = reframework
local imgui = imgui

local module = {}

module.name = "Training Parameters Plus"
module.description =
    "Module for adjusting training mode parameters. Modifiable parameters include: Health, Drive, Super, Position, Distance, Unique Resources"

module.data = {}
module.ui = {}

module.data.UniqueCharData = require("TrainingModePlusScripts/UniqueCharacterParametersData")
module.data.PositionParametersData = require("TrainingModePlusScripts/PositionParametersData")

function module.ui.init(playerUI, playerParam)
    --[[
        init health ui data
    ]]
    playerUI.health = {}
    -- set the health percentage to the player's vital point
    playerUI.health.percentage = playerParam.Vital_Point
    -- changed for percentage slider change
    playerUI.health.percentage_changed = false

    --[[
        init drive ui data
    ]]
    playerUI.drive = {}

    -- stock drive bars
    -- checks if the drive type is stock or points
    playerUI.drive.type_stock = playerParam.DG_Type == 1
    -- boolean variable for type change
    playerUI.drive.type_changed = false

    -- stock values
    playerUI.drive.drive_stocks = playerParam.DG_Stock
    -- changed for drive stocks slider change
    playerUI.drive.drive_stocks_changed = false

    -- points values
    -- checkbox for enabling the precise drive points adjustment
    playerUI.drive.type_points_enabled = false
    -- changed for drive type change
    playerUI.drive.type_points_enabled_changed = false

    -- set the drive points to the player's drive gauge points
    playerUI.drive.points = playerParam.DG_Point
    -- changed for drive points slider change
    playerUI.drive.points_changed = false

    -- burnout checkbox
    playerUI.drive.burnout = false
    playerUI.drive.burnout_changed = false

    --[[
        init super ui data
    ]]
    playerUI.super = {}

    -- stock super bars
    -- checks if the super type is stock or points
    playerUI.super.type_stock = playerParam.SA_Type == 1
    -- boolean variable for type change
    playerUI.super.type_changed = false

    -- stock values
    playerUI.super.super_stocks = playerParam.SA_Stock
    -- changed for super stocks slider change
    playerUI.super.super_stocks_changed = false

    -- points values
    -- checkbox for enabling the precise super points adjustment
    playerUI.super.type_points_enabled = false
    -- changed for super type change
    playerUI.super.type_points_enabled_changed = false

    -- set the super points to the player's super gauge points
    playerUI.super.points = playerParam.SA_Point
    -- changed for super points slider change
    playerUI.super.points_changed = false
end

function module.ui.update(playerUI, playerParam)
    local need_update = false

    --[[
        update health ui data
    ]]
    if playerUI.health.percentage_changed then
        playerParam.Vital_Point = playerUI.health.percentage
        need_update = true
    end

    --[[
        update drive ui data
    ]]
    -- update drive type
    if playerUI.drive.type_changed then
        if playerUI.drive.type_stock then
            playerParam.DG_Type = 1
        else
            playerParam.DG_Type = 3
            playerParam.Is_DG_Point_Lock = true
        end
        need_update = true
    end

    -- update drive stocks
    if playerUI.drive.drive_stocks_changed then
        playerParam.DG_Stock = playerUI.drive.drive_stocks
        playerParam.DG_Point = playerUI.drive.drive_stocks * 10000
        need_update = true
    end

    -- update drive points
    if playerUI.drive.points_changed then
        playerParam.DG_Point = playerUI.drive.points
        -- round to nearest stock
        playerParam.DG_Stock = math.floor((playerUI.drive.points + 5000) / 10000)
        need_update = true
    end

    -- update burnout
    if playerUI.drive.burnout_changed then
        playerParam.Is_DG_Break = playerUI.drive.burnout
        need_update = true
    end

    --[[
        update super ui data
    ]]
    -- update super type
    if playerUI.super.type_changed then
        if playerUI.super.type_stock then
            playerParam.SA_Type = 1
        else
            playerParam.SA_Type = 2
            playerParam.Is_SA_Point_Lock = true
        end
        need_update = true
    end

    -- update super stocks
    if playerUI.super.super_stocks_changed then
        playerParam.SA_Stock = playerUI.super.super_stocks
        playerParam.SA_Point = playerUI.super.super_stocks * 10000
        need_update = true
    end
    -- update super points
    if playerUI.super.points_changed then
        playerParam.SA_Point = playerUI.super.points
        -- round to nearest stock
        playerParam.SA_Stock = math.floor(playerUI.super.points / 10000)
        need_update = true
    end

    --[[
        unique character gauges 
    ]]
    return need_update
end

function module.ui.init_unique_gauges()
    -- initialize unique character gauges ui data
    module.ui.unique = {}
    -- initialize the unique data based on the required unique data
    for _, charData in pairs(module.data.UniqueCharData) do
        module.ui.unique[charData.name] = {}
        -- initialize timers ui data
        if charData.timers then
            for _, timerData in pairs(charData.timers) do
                module.ui.unique[charData.name][timerData.id] = {}
                module.ui.unique[charData.name][timerData.id].timer_combo_value = 1
                module.ui.unique[charData.name][timerData.id].timer_combo_changed = false
                if timerData.install == true then
                    module.ui.unique[charData.name][timerData.id].installed_start_value = timerData.timerMaxValue
                    module.ui.unique[charData.name][timerData.id].installed_start_value_changed = false
                end
            end
        end
        -- initialize stocks ui data
        if charData.stocks then
            for _, stockData in pairs(charData.stocks) do
                module.ui.unique[charData.name][stockData.id] = {}
                module.ui.unique[charData.name][stockData.id].stock_slider = 0
                module.ui.unique[charData.name][stockData.id].stock_slider_changed = false
                module.ui.unique[charData.name][stockData.id].infinite_checkbox = false
                module.ui.unique[charData.name][stockData.id].infinite_checkbox_changed = false
            end
        end
    end
end

function module.ui.update_unique_gauges()
    -- to be implemented later
    local need_refresh = false

    local char_id1 = module.data.SelectMenu.PlayerDatas[0].FighterID
    local char_id2 = module.data.SelectMenu.PlayerDatas[1].FighterID
    local char_datas
    if char_id1 == char_id2 then
        char_datas = {module.data.UniqueCharData[char_id1]}
    else
        char_datas = {module.data.UniqueCharData[char_id1], module.data.UniqueCharData[char_id2]}
    end

    for index, char_data in pairs(char_datas) do
        if char_data then
            -- update timers
            if char_data.timers then
                for _, timerData in pairs(char_data.timers) do
                    local ui_timer = module.ui.unique[char_data.name][timerData.id]
                    if ui_timer.timer_combo_changed then
                        -- set the unique gauge data based on the selected value
                        module.data.UniqueGaugeData[timerData.id] = ui_timer.timer_combo_value - 1
                        need_refresh = true
                    end
                    -- add logic for the timer later
                    if timerData.install == true and ui_timer.timer_combo_value == 2 then
                        -- set the installed start value somewhere
                        liveData = nil
                        if index == 1 then
                            liveData = module.data.live_P1
                        else
                            liveData = module.data.live_P2
                        end
                        if module.data.sGame.stage_timer == 1 then
                            liveData.style_timer = ui_timer.installed_start_value
                        end
                    end
                    if ui_timer.installed_start_value_changed then
                        need_refresh = true
                    end
                end
            end

            -- update stocks
            if char_data.stocks then
                for _, stockData in pairs(char_data.stocks) do
                    local ui_stock = module.ui.unique[char_data.name][stockData.id]
                    if ui_stock.infinite_checkbox_changed then
                        if ui_stock.infinite_checkbox then
                            module.data.UniqueGaugeData[stockData.id] = 7
                        else
                            module.data.UniqueGaugeData[stockData.id] = ui_stock.stock_slider
                        end
                        need_refresh = true
                    end
                    if ui_stock.stock_slider_changed then
                        module.data.UniqueGaugeData[stockData.id] = ui_stock.stock_slider
                        need_refresh = true
                    end
                end
            end
        end
    end

    return need_refresh
end

function module.ui.init_position()
    module.ui.position = {}

    --[[ 
        initialize relative position ui data
    ]]
    module.ui.position.relative_distance_changed = false

    local offset1 = module.data.PositionParametersData.character_relative_distance_offsets[char_id1] or 0.0
    local offset2 = module.data.PositionParametersData.character_relative_distance_offsets[char_id2] or 0.0
    local total_offset = offset1 + offset2

    module.ui.position.relative_distance_min =
        module.data.PositionParametersData.default_relative_distance.min + total_offset
    module.ui.position.relative_distance_max =
        module.data.PositionParametersData.default_relative_distance.max - total_offset
    if module.data.SelectMenu.StartLocation == 0 then
        -- center pivot
        module.ui.position.relative_distance = 300.0
        module.ui.position.discrete_relative_distance_preset_index = 6
    else
        module.ui.position.relative_distance = module.ui.position.relative_distance_min
        module.ui.position.discrete_relative_distance_preset_index = 1
    end

    -- checkbox for enabling precise relative distance adjustment
    module.ui.position.relative_distance_enabled = false
    module.ui.position.relative_distance_enabled_changed = false

    -- checkbox for enabling/disabling discrete relative distance adjustments
    module.ui.position.discrete_relative_distance_enabled = true
    module.ui.position.discrete_relative_distance_enabled_changed = false

    -- state variables of the combo picker for the discrete relative distance
    module.ui.position.discrete_relative_distance_preset_index_changed = false

    -- store old starting position for disabling and proper calculations
    module.ui.position.old_starting_position = module.data.SelectMenu.StartLocation

    --[[
        Starting position initialization
    ]]
    -- checkbox for enabling starting position adjustment
    module.ui.position.starting_position_enabled = false
    module.ui.position.starting_position_enabled_changed = false

    -- float value for the actual starting position
    module.ui.position.starting_position_value = 0.0
    module.ui.position.starting_position_value_changed = false

    -- combo for type of starting position: (center pivot point, P1 pivot, P2 pivot)
    module.ui.position.starting_position_pivot_type_index = 1
    module.ui.position.starting_position_pivot_type_index_changed = false

    -- combo for "from where" to calculate the distance (absolute position, distance from left corner, distance from right corner)
    module.ui.position.starting_position_distance_reference_index = 1
    module.ui.position.starting_position_distance_reference_index_changed = false
    module.ui.position.starting_position_distance_reference_previous_index = 1

    -- checkbox for discrete reference point adjustment toggle (from discrete presets or manual adjustment)
    module.ui.position.starting_position_discrete_reference_enabled = true
    module.ui.position.starting_position_discrete_reference_enabled_changed = false

    -- integer value for the slider of the discrete reference point
    module.ui.position.starting_position_discrete_reference_value = 0
    module.ui.position.starting_position_discrete_reference_value_changed = false

    -- flag to show warning if the position is invalid
    module.ui.position.show_position_warning = false

    --[[
        HOOKS
    ]]
    local function on_pre(args)
        -- no pre logic needed
    end
    local function on_post(retval)
        if not module.ui.position.relative_distance_enabled then
            -- if its disabled
            return
        end

        log.debug(
            module.data.SelectMenu.StartLocation .. " old pos: " .. tostring(module.ui.position.old_starting_position)
        )

        -- get the current characters to determine the offset to the relative distances
        local char_id1 = module.data.SelectMenu.PlayerDatas[0].FighterID
        local char_id2 = module.data.SelectMenu.PlayerDatas[1].FighterID

        local offset1 = module.data.PositionParametersData.character_relative_distance_offsets[char_id1] or 0.0
        local offset2 = module.data.PositionParametersData.character_relative_distance_offsets[char_id2] or 0.0
        local total_offset = offset1 + offset2

        if module.ui.position.starting_position_enabled then
            module.data.SelectMenu.StartLocation = 3
        else
            if module.data.SelectMenu.StartLocation ~= 3 then
                -- if starting position adjustment is disabled, we just use the old starting position
                module.ui.position.old_starting_position = module.data.SelectMenu.StartLocation
            else
                -- if starting position adjustment is disabled, we just use the old starting position
                module.data.SelectMenu.StartLocation = module.ui.position.old_starting_position
            end
        end

        if module.data.SelectMenu.StartLocation == 0 then
            -- center pivot
            local center_position = (module.ui.position.relative_distance) / 2.0
            module.data.SelectMenu.PlayerDatas[0].ManualPosX = -center_position - (total_offset / 2.0)
            module.data.SelectMenu.PlayerDatas[1].ManualPosX = center_position + (total_offset / 2.0)
        elseif module.data.SelectMenu.StartLocation == 1 then
            -- right side pivot
            local right_position = module.data.PositionParametersData.default_screen_position.max
            module.data.SelectMenu.PlayerDatas[0].ManualPosX =
                right_position - module.ui.position.relative_distance - total_offset
            module.data.SelectMenu.PlayerDatas[1].ManualPosX = right_position
        elseif module.data.SelectMenu.StartLocation == 2 then
            -- left side pivot
            local left_position = module.data.PositionParametersData.default_screen_position.min
            module.data.SelectMenu.PlayerDatas[0].ManualPosX = left_position
            module.data.SelectMenu.PlayerDatas[1].ManualPosX =
                left_position + module.ui.position.relative_distance + total_offset
        elseif module.data.SelectMenu.StartLocation == 3 then
            -- custom position pivot
            local new_pos1
            local new_pos2

            -- first, calculate the position of the fulcrum based on the distance reference
            local fulcrum_position = 0.0
            if module.ui.position.starting_position_distance_reference_index == 1 then
                -- absolute position
                fulcrum_position = module.ui.position.starting_position_value
            elseif module.ui.position.starting_position_distance_reference_index == 2 then
                -- distance from left corner
                fulcrum_position =
                    module.ui.position.starting_position_value +
                    module.data.PositionParametersData.default_screen_position.min
            elseif module.ui.position.starting_position_distance_reference_index == 3 then
                -- distance from right corner
                fulcrum_position =
                    module.data.PositionParametersData.default_screen_position.max -
                    module.ui.position.starting_position_value
            end

            -- calculate the new positions based on the pivot type
            if module.ui.position.starting_position_pivot_type_index == 1 then
                -- center pivot type
                new_pos1 = fulcrum_position - (module.ui.position.relative_distance / 2.0)
                new_pos2 = fulcrum_position + (module.ui.position.relative_distance / 2.0)
            elseif module.ui.position.starting_position_pivot_type_index == 2 then
                -- p1 player pivot type
                new_pos1 = fulcrum_position
                new_pos2 = fulcrum_position + module.ui.position.relative_distance
            elseif module.ui.position.starting_position_pivot_type_index == 3 then
                -- p2 player pivot type
                new_pos1 = fulcrum_position - module.ui.position.relative_distance
                new_pos2 = fulcrum_position
            end

            -- check screen bounds
            local screen_min = module.data.PositionParametersData.default_screen_position.min
            local screen_max = module.data.PositionParametersData.default_screen_position.max
            if new_pos1 < screen_min then
                new_pos1 = screen_min
                new_pos2 = screen_min + module.ui.position.relative_distance
            elseif new_pos2 > screen_max then
                new_pos2 = screen_max
                new_pos1 = screen_max - module.ui.position.relative_distance
            end
            module.data.SelectMenu.PlayerDatas[0].ManualPosX = new_pos1 - (total_offset / 2.0)
            module.data.SelectMenu.PlayerDatas[1].ManualPosX = new_pos2 + (total_offset / 2.0)
        end

        module.data.SelectMenu.StartLocation = 3
    end

    sdk.hook(
        sdk.find_type_definition("app.training.tf_SelectMenu.FuncData"):get_method("ChangeStartLocationType"),
        on_pre,
        on_post
    )
end

function module.ui.update_position()
    -- get the current characters to determine the offset to the relative distances
    local char_id1 = module.data.SelectMenu.PlayerDatas[0].FighterID
    local char_id2 = module.data.SelectMenu.PlayerDatas[1].FighterID

    local offset1 = module.data.PositionParametersData.character_relative_distance_offsets[char_id1] or 0.0
    local offset2 = module.data.PositionParametersData.character_relative_distance_offsets[char_id2] or 0.0
    local total_offset = offset1 + offset2

    local need_refresh = false

    module.ui.position.relative_distance_min =
        module.data.PositionParametersData.default_relative_distance.min + total_offset
    module.ui.position.relative_distance_max =
        module.data.PositionParametersData.default_relative_distance.max - total_offset
    module.ui.position.relative_distance =
        math.min(
        math.max(module.ui.position.relative_distance, module.ui.position.relative_distance_min),
        module.ui.position.relative_distance_max
    )

    if module.ui.position.discrete_relative_distance_preset_index_changed then
        need_refresh = true
        -- set the relative distance to the preset value
        if
            module.ui.position.discrete_relative_distance_preset_index ==
                #module.data.PositionParametersData.preset_relative_distance_offsets.values + 1
         then
            module.ui.position.relative_distance = module.ui.position.relative_distance_max
        else
            module.ui.position.relative_distance =
                module.ui.position.relative_distance_min +
                module.data.PositionParametersData.preset_relative_distance_offsets.values[
                    module.ui.position.discrete_relative_distance_preset_index
                ]
        end
    end

    if module.ui.position.relative_distance_enabled_changed then
        need_refresh = true
        -- this means we either enabled or disabled relative distance adjustment
        if module.ui.position.relative_distance_enabled then
            -- first store the old starting position
            module.ui.position.old_starting_position = module.data.SelectMenu.StartLocation
        else
            -- if we disabled it, we should restore the old starting position
            module.data.SelectMenu.StartLocation = module.ui.position.old_starting_position
        end
    end

    if module.ui.position.starting_position_enabled_changed then
        need_refresh = true
        -- if we disabled starting position adjustment, restore the old starting position
        if module.ui.position.starting_position_enabled then
            module.ui.position.old_starting_position = 3
        else
            -- when we disable starting position, we just default back to the middle of the screen, cuz it don't matter
            module.ui.position.old_starting_position = 0
        end
    end

    if module.ui.position.starting_position_discrete_reference_value_changed then
        -- set the actual starting position value to the discrete value but scaling it properly
        -- values go from -6 to 6, mapping to min and max screen positions
        local min_pos = module.data.PositionParametersData.default_screen_position.min
        local max_pos = module.data.PositionParametersData.default_screen_position.max
        local discrete_value = module.ui.position.starting_position_discrete_reference_value
        -- -6 to 6 is 13 steps
        module.ui.position.starting_position_value = min_pos + ((discrete_value + 6) / 12) * (max_pos - min_pos)
    end

    -- when the combo picker for the reference point, we adjust the value, such that it corresponds to the same position
    if module.ui.position.starting_position_distance_reference_index_changed then
        need_refresh = true
        local previous_index = module.ui.position.starting_position_distance_reference_previous_index
        local current_value = module.ui.position.starting_position_value

        -- first, calculate the fulcrum position based on the previous index
        local fulcrum_position = 0.0
        if previous_index == 1 then
            -- absolute position
            fulcrum_position = current_value
        elseif previous_index == 2 then
            -- distance from left corner
            fulcrum_position = current_value + module.data.PositionParametersData.default_screen_position.min
        elseif previous_index == 3 then
            -- distance from right corner
            fulcrum_position = module.data.PositionParametersData.default_screen_position.max - current_value
        end

        -- now, based on the new index, calculate the new starting position value
        if module.ui.position.starting_position_distance_reference_index == 1 then
            -- absolute position
            module.ui.position.starting_position_value = fulcrum_position
        elseif module.ui.position.starting_position_distance_reference_index == 2 then
            -- distance from left corner
            module.ui.position.starting_position_value =
                fulcrum_position - module.data.PositionParametersData.default_screen_position.min
        elseif module.ui.position.starting_position_distance_reference_index == 3 then
            -- distance from right corner
            module.ui.position.starting_position_value =
                module.data.PositionParametersData.default_screen_position.max - fulcrum_position
        end

        module.ui.position.starting_position_distance_reference_previous_index =
            module.ui.position.starting_position_distance_reference_index
    end

    -- verify if the starting position values are valid, if not, show a warning
    if
        module.ui.position.starting_position_value_changed or module.ui.position.relative_distance_changed or
            module.ui.position.discrete_relative_distance_preset_index_changed or
            module.ui.position.starting_position_pivot_type_index_changed or
            module.ui.position.starting_position_distance_reference_index_changed or
            module.ui.position.starting_position_discrete_reference_value_changed
     then
        -- first, calculate the position of the fulcrum based on the distance reference
        local fulcrum_position = 0.0
        if module.ui.position.starting_position_distance_reference_index == 1 then
            -- absolute position
            fulcrum_position = module.ui.position.starting_position_value
        elseif module.ui.position.starting_position_distance_reference_index == 2 then
            -- distance from left corner
            fulcrum_position =
                module.ui.position.starting_position_value +
                module.data.PositionParametersData.default_screen_position.min
        elseif module.ui.position.starting_position_distance_reference_index == 3 then
            -- distance from right corner
            fulcrum_position =
                module.data.PositionParametersData.default_screen_position.max -
                module.ui.position.starting_position_value
        end

        -- update the discrete value based on the actual starting position value
        local min_pos = module.data.PositionParametersData.default_screen_position.min
        local max_pos = module.data.PositionParametersData.default_screen_position.max
        local fulcrum_relative_position = fulcrum_position - min_pos
        local relative_range = max_pos - min_pos
        local discrete_value = math.floor((fulcrum_relative_position / relative_range) * 12 + 0.5) - 6
        module.ui.position.starting_position_discrete_reference_value = discrete_value

        -- based on the pivot type, check if the positions are valid
        local new_pos1
        local new_pos2
        if module.ui.position.starting_position_pivot_type_index == 1 then
            -- center pivot type
            new_pos1 = fulcrum_position - (module.ui.position.relative_distance / 2.0)
            new_pos2 = fulcrum_position + (module.ui.position.relative_distance / 2.0)
        elseif module.ui.position.starting_position_pivot_type_index == 2 then
            -- p1 player pivot type
            new_pos1 = fulcrum_position
            new_pos2 = fulcrum_position + module.ui.position.relative_distance
        elseif module.ui.position.starting_position_pivot_type_index == 3 then
            -- p2 player pivot type
            new_pos1 = fulcrum_position - module.ui.position.relative_distance
            new_pos2 = fulcrum_position
        end
        -- check screen bounds
        local screen_min = module.data.PositionParametersData.default_screen_position.min
        local screen_max = module.data.PositionParametersData.default_screen_position.max
        if new_pos1 < screen_min or new_pos2 > screen_max then
            module.ui.position.show_position_warning = true
        else
            module.ui.position.show_position_warning = false
        end
    end

    return need_refresh
end

function module.init()
    -- get the important fields at init time
    module.data.TrainingManager = sdk.get_managed_singleton("app.training.TrainingManager")
    module.data.TrainingData = module.data.TrainingManager:get_field("_tData")
    module.data.ParameterSetting = module.data.TrainingData:get_field("ParameterSetting")
    module.data.tf_PS = module.data.TrainingManager._tfFuncs._entries[6]:get_field("value")
    module.data.tf_SM = module.data.TrainingManager._tfFuncs._entries[0]:get_field("value")
    module.data.P1Param = module.data.ParameterSetting.PlayerDatas[0]
    module.data.P2Param = module.data.ParameterSetting.PlayerDatas[1]
    module.data.UniqueGaugeData = module.data.ParameterSetting.UniqueData
    module.data.SelectMenu = module.data.TrainingData:get_field("SelectMenu")
    local gBattle = sdk.find_type_definition("gBattle")
    local sPlayer = gBattle:get_field("Player"):get_data(nil)
    local cPlayer = sPlayer.mcPlayer
    -- use sGame.stage_timer == 1 to check for the refresh (you can apply all the settings you want here and they won't get overwritten by the game at this point)
    module.data.sGame = gBattle:get_field("Game"):get_data(nil)
    module.data.live_P1 = cPlayer[0]
    module.data.live_P2 = cPlayer[1]

    module.data.start_tracking = 0

    local char_id1 = module.data.SelectMenu.PlayerDatas[0].FighterID
    local char_id2 = module.data.SelectMenu.PlayerDatas[1].FighterID

    module.types = {}
    module.types.sfix3 = sdk.find_type_definition("via.Sfix3")

    -- initialize ui data
    module.ui.p1 = {}
    module.ui.p2 = {}
    module.ui.init(module.ui.p1, module.data.P1Param)
    module.ui.init(module.ui.p2, module.data.P2Param)
    module.ui.init_unique_gauges()
    module.ui.init_position()
end

function module.on_frame()
    -- update live data references

    need_update = false
    need_refresh = false

    need_update = need_update or module.ui.update(module.ui.p1, module.data.P1Param)
    need_update = need_update or module.ui.update(module.ui.p2, module.data.P2Param)

    -- unique character gauges update
    need_refresh = module.ui.update_unique_gauges() or need_refresh

    -- position update
    module.ui.update_position()

    -- updates the training mode immediately rather than waiting for refresh or whatever
    if need_update then
        sdk.call_object_func(module.data.tf_PS, "bApply")
    end

    -- refreshes training mode immediately
    if need_refresh then
        module.data.TrainingManager._IsReqRefresh = true
    end
end

-- UI rendering helpers
function module.ui.draw_drive(playerUI, playerLabel, playerParam)
    -- Drive
    type_value = playerParam.DG_Type == 1
    playerUI.drive.type_changed, playerUI.drive.type_stock = imgui.checkbox(playerLabel .. " Drive Stock", type_value)

    if playerUI.drive.type_stock then
        playerUI.drive.drive_stocks_changed, playerUI.drive.drive_stocks =
            imgui.slider_int(playerLabel .. " Drive Stocks", playerParam.DG_Stock, 0, 6)
    else
        playerUI.drive.type_points_enabled_changed, playerUI.drive.type_points_enabled =
            imgui.checkbox(
            playerLabel .. " Enable pointwise drive gauge adjustment",
            playerUI.drive.type_points_enabled
        )

        if playerUI.drive.type_points_enabled then
            playerUI.drive.points_changed, playerUI.drive.points =
                imgui.drag_int(playerLabel .. " Drive Points", playerParam.DG_Point, 1, 0, 60000)
        else
            points_increments = 0
            current_points = playerParam.DG_Point / 10000
            playerUI.drive.points_changed, points_increments =
                imgui.slider_float(
                playerLabel .. " Drive Points (stock increments of 10%)",
                current_points,
                0,
                6,
                "%.1f"
            )
            -- convert to points
            playerUI.drive.points = math.floor(points_increments * 10000)
        end
    end
    playerUI.drive.burnout_changed, playerUI.drive.burnout =
        imgui.checkbox(playerLabel .. " Burnout", playerParam.Is_DG_Break)
end

function module.ui.draw_super(playerUI, playerLabel, playerParam)
    -- Super UI elements would go here
    type_value = playerParam.SA_Type == 1
    playerUI.super.type_changed, playerUI.super.type_stock = imgui.checkbox(playerLabel .. " Super Stock", type_value)

    if playerUI.super.type_stock then
        playerUI.super.super_stocks_changed, playerUI.super.super_stocks =
            imgui.slider_int(playerLabel .. " Super Stocks", playerParam.SA_Stock, 0, 3)
    else
        playerUI.super.type_points_enabled_changed, playerUI.super.type_points_enabled =
            imgui.checkbox(
            playerLabel .. " Enable pointwise super gauge adjustment",
            playerUI.super.type_points_enabled
        )

        if playerUI.super.type_points_enabled then
            playerUI.super.points_changed, playerUI.super.points =
                imgui.drag_int(playerLabel .. " Super Points", playerParam.SA_Point, 1, 0, 30000)
        else
            points_increments = 0
            current_points = playerParam.SA_Point / 10000
            playerUI.super.points_changed, points_increments =
                imgui.slider_float(
                playerLabel .. " Super Points (stock increments of 1%)",
                current_points,
                0,
                3,
                "%.2f"
            )
            -- convert to points
            playerUI.super.points = math.floor(points_increments * 10000)
        end
    end
end

function module.ui.draw_unique_character_gauges()
    local char_id1 = module.data.SelectMenu.PlayerDatas[0].FighterID
    local char_id2 = module.data.SelectMenu.PlayerDatas[1].FighterID
    local char_datas
    if char_id1 == char_id2 then
        char_datas = {module.data.UniqueCharData[char_id1]}
    else
        char_datas = {module.data.UniqueCharData[char_id1], module.data.UniqueCharData[char_id2]}
    end

    local any_installed_timer = false

    for _, char_data in pairs(char_datas) do
        if char_data then
            any_installed_timer = true
            imgui.text("Character: " .. char_data.name)
            -- draw timers
            if char_data.timers then
                for _, timerData in pairs(char_data.timers) do
                    local current_value = module.data.UniqueGaugeData[timerData.id]
                    -- use stored timer ui values
                    local ui_timer = module.ui.unique[char_data.name][timerData.id]
                    local descriptor = timerData.descriptors[current_value + 1]
                    imgui.text(timerData.name .. ": " .. descriptor)

                    -- use stored slider value as current so it persists
                    ui_timer.timer_combo_changed, ui_timer.timer_combo_value =
                        imgui.combo(timerData.name .. " Value ", current_value + 1, timerData.descriptors)
                    if timerData.install == true and current_value == 1 then
                        -- installed timer UI elements
                        ui_timer.installed_start_value_changed, ui_timer.installed_start_value =
                            imgui.slider_int(
                            timerData.name .. " starting activation value",
                            ui_timer.installed_start_value,
                            0,
                            timerData.timerMaxValue
                        )
                    end
                end
            end

            -- draw stocks
            if char_data.stocks then
                for _, stockData in pairs(char_data.stocks) do
                    local current_value = module.data.UniqueGaugeData[stockData.id]
                    -- check for value == 7 (infinite)
                    local descriptor
                    if current_value == 7 then
                        descriptor = "Infinite"
                    else
                        descriptor = stockData.descriptors[current_value + 1]
                    end
                    -- use stored stock ui values
                    local ui_stock = module.ui.unique[char_data.name][stockData.id]
                    imgui.text(stockData.name .. ": " .. descriptor)

                    if stockData.allowInfinite then
                        ui_stock.infinite_checkbox_changed, ui_stock.infinite_checkbox =
                            imgui.checkbox("Toggle Infinite " .. stockData.name, current_value == 7)
                    end
                    -- if infinite is enabled, don't show the slider
                    if current_value ~= 7 then
                        ui_stock.stock_slider_changed, ui_stock.stock_slider =
                            imgui.slider_int(
                            stockData.name .. " Value",
                            current_value,
                            stockData.minValue,
                            stockData.maxValue
                        )
                        if not stockData.correspond then
                            imgui.text_colored(
                                "Warning: The values on the slider do not correspond to the ingame values, consult the '" ..
                                    stockData.name .. ": #' value instead",
                                0xFF00A9F9
                            )
                        end
                    end
                end
            end
        end
    end
    if any_installed_timer then
        imgui.text_colored("Not available for these characters", 0xFF00A9F9)
    end
    imgui.separator()
end

function module.ui.draw_relative_position()
    module.ui.position.relative_distance_enabled_changed, module.ui.position.relative_distance_enabled =
        imgui.checkbox("Enable Start Position Adjustment", module.ui.position.relative_distance_enabled)

    imgui.separator()

    if module.ui.position.relative_distance_enabled then
        module.ui.position.discrete_relative_distance_enabled_changed,
            module.ui.position.discrete_relative_distance_enabled =
            imgui.checkbox("Use Preset Relative Distances", module.ui.position.discrete_relative_distance_enabled)

        if module.ui.position.discrete_relative_distance_enabled then
            -- combo picker for preset relative distances
            module.ui.position.discrete_relative_distance_preset_index_changed,
                module.ui.position.discrete_relative_distance_preset_index =
                imgui.combo(
                "Relative Distance Presets",
                module.ui.position.discrete_relative_distance_preset_index,
                module.data.PositionParametersData.preset_relative_distance_offsets.names
            )
            imgui.text("Current Relative Distance: " .. string.format("%.2f", module.ui.position.relative_distance))
        else
            module.ui.position.relative_distance_changed, module.ui.position.relative_distance =
                imgui.drag_float(
                "Relative Distance",
                module.ui.position.relative_distance,
                1.0,
                module.ui.position.relative_distance_min,
                module.ui.position.relative_distance_max
            )
        end

        imgui.separator()

        -- checkbox for enabling starting position adjustment
        module.ui.position.starting_position_enabled_changed, module.ui.position.starting_position_enabled =
            imgui.checkbox("Enable Starting Position Adjustment", module.ui.position.starting_position_enabled)
        if module.ui.position.starting_position_enabled then
            -- combo picker for type of starting position
            module.ui.position.starting_position_pivot_type_index_changed,
                module.ui.position.starting_position_pivot_type_index =
                imgui.combo(
                "Starting Position Pivot Type",
                module.ui.position.starting_position_pivot_type_index,
                {
                    "Center Pivot Point",
                    "Left Player Position Pivot (P1 Side)",
                    "Right Player Position Pivot (P2 Side)"
                }
            )

            -- checkbox for discrete reference point adjustment toggle
            module.ui.position.starting_position_discrete_reference_enabled_changed,
                module.ui.position.starting_position_discrete_reference_enabled =
                imgui.checkbox(
                "Use Discrete Starting Position Reference",
                module.ui.position.starting_position_discrete_reference_enabled
            )

            if module.ui.position.starting_position_discrete_reference_enabled then
                -- slider for discrete reference point
                module.ui.position.starting_position_discrete_reference_value_changed,
                    module.ui.position.starting_position_discrete_reference_value =
                    imgui.slider_int(
                    "Starting Position Reference",
                    module.ui.position.starting_position_discrete_reference_value,
                    -6,
                    6
                )
            else
                -- combo picker for distance reference
                module.ui.position.starting_position_distance_reference_index_changed,
                    module.ui.position.starting_position_distance_reference_index =
                    imgui.combo(
                    "Starting Position Distance Reference",
                    module.ui.position.starting_position_distance_reference_index,
                    {
                        "Absolute Position",
                        "Distance from Left Corner",
                        "Distance from Right Corner"
                    }
                )

                -- slider for starting position

                -- if the reference is not absolute position, adjust the min/max accordingly
                local starting_position_min = 0.0
                local starting_position_max = 0.0
                if module.ui.position.starting_position_distance_reference_index == 1 then
                    -- absolute position
                    starting_position_min = module.data.PositionParametersData.default_screen_position.min
                    starting_position_max = module.data.PositionParametersData.default_screen_position.max
                else
                    -- distance from a corner
                    starting_position_min = 0.0
                    starting_position_max =
                        module.data.PositionParametersData.default_screen_position.max -
                        module.data.PositionParametersData.default_screen_position.min
                end
                module.ui.position.starting_position_value_changed, module.ui.position.starting_position_value =
                    imgui.drag_float(
                    "Starting Position X",
                    module.ui.position.starting_position_value,
                    1.0,
                    starting_position_min,
                    starting_position_max
                )
            end

            if module.ui.position.show_position_warning then
                imgui.text_colored(
                    "Warning: The current combination of relative distance and starting position would cause the character to go offscreen!\nThe position will be adjust to respect the relative position and screen bounds.",
                    0xFF00A9F9
                )
            end
        end
    end
end

function module.ui.draw_starting_position()
end

-- UI rendering function
function module.draw_ui()
    if imgui.collapsing_header("Training Parameters") then
        -- Health
        if imgui.tree_node("Health") then
            module.ui.p1.health.percentage_changed, module.ui.p1.health.percentage =
                imgui.slider_int("P1 Health Percentage", module.data.P1Param.Vital_Point, 0, 100)
            imgui.spacing()

            module.ui.p2.health.percentage_changed, module.ui.p2.health.percentage =
                imgui.slider_int("P2 Health Percentage", module.data.P2Param.Vital_Point, 0, 100)
            imgui.tree_pop()
        end

        -- Drive
        if imgui.tree_node("Drive") then
            module.ui.draw_drive(module.ui.p1, "P1", module.data.P1Param)
            imgui.separator()
            module.ui.draw_drive(module.ui.p2, "P2", module.data.P2Param)
            imgui.tree_pop()
        end

        -- Super
        if imgui.tree_node("Super") then
            module.ui.draw_super(module.ui.p1, "P1", module.data.P1Param)
            imgui.separator()
            module.ui.draw_super(module.ui.p2, "P2", module.data.P2Param)
            imgui.tree_pop()
        end

        -- Unique Character Gauges
        if imgui.tree_node("Unique Character Gauges") then
            module.ui.draw_unique_character_gauges()
            imgui.tree_pop()
        end

        -- Starting Player Position
        if imgui.tree_node("Starting Player Position") then
            module.ui.draw_relative_position()
            imgui.tree_pop()
        end
    end
end

return module

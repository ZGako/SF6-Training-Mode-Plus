-- intellinsense
local re = re
local sdk = sdk
local reframework = reframework 
local imgui = imgui

local module = {}

module.name = "Training Parameters Plus"
module.description = "Module for adjusting training mode parameters. Modifiable parameters include: Health, Drive, Super, Position, Distance, Unique Resources"

module.data = {}
module.ui = {}

module.data.UniqueCharData = require("TrainingModePlusScripts/UniqueCharacterParametersData")

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

function module.init()
    
    -- get the important fields at init time
    module.data.TrainingManager = sdk.get_managed_singleton("app.training.TrainingManager")
    module.data.TrainingData = module.data.TrainingManager:get_field("_tData")
    module.data.ParameterSetting = module.data.TrainingData:get_field("ParameterSetting")
    module.data.tf_PS = module.data.TrainingManager._tfFuncs._entries[6]:get_field("value")
    module.data.P1Param = module.data.ParameterSetting.PlayerDatas[0]
    module.data.P2Param = module.data.ParameterSetting.PlayerDatas[1]
    module.data.UniqueGaugeData = module.data.ParameterSetting.UniqueGaugeData
    module.data.SelectMenu = module.data.TrainingData:get_field("SelectMenu")
    local gBattle = sdk.find_type_definition("gBattle")
    local sPlayer = gBattle:get_field("Player"):get_data(nil)
    local cPlayer = sPlayer.mcPlayer
    module.data.live_P1 = cPlayer[0]
    module.data.live_P2 = cPlayer[1]

    -- initialize ui data
    module.ui.p1 = {}
    module.ui.p2 = {}
    module.ui.init(module.ui.p1, module.data.P1Param)
    module.ui.init(module.ui.p2, module.data.P2Param)
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
                    module.ui.unique[charData.name][timerData.id].installed_start_value = nil
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

function module.on_frame()
    -- update live data references

    need_update = false

    need_update = need_update or module.ui.update(module.ui.p1, module.data.P1Param)
    need_update = need_update or module.ui.update(module.ui.p2, module.data.P2Param)

    -- unique character gauges update
    

    -- updates the training mode immediately rather than waiting for refresh or whatever
    if need_update then
        sdk.call_object_func(module.data.tf_PS, "bApply")
    end

end

-- UI rendering helpers
function module.ui.draw_drive(playerUI, playerLabel, playerParam)
    -- Drive
    type_value = playerParam.DG_Type == 1
    playerUI.drive.type_changed, playerUI.drive.type_stock = imgui.checkbox(playerLabel .. " Drive Stock", type_value)
    
    if playerUI.drive.type_stock then 
        playerUI.drive.drive_stocks_changed, playerUI.drive.drive_stocks = imgui.slider_int(playerLabel .. " Drive Stocks", playerParam.DG_Stock, 0, 6)
    else
        playerUI.drive.type_points_enabled_changed, playerUI.drive.type_points_enabled = imgui.checkbox(playerLabel .. " Enable pointwise drive gauge adjustment", playerUI.drive.type_points_enabled)
        
        if playerUI.drive.type_points_enabled then
            playerUI.drive.points_changed, playerUI.drive.points = imgui.drag_int(playerLabel .. " Drive Points", playerParam.DG_Point, 1, 0, 60000)
        else
            points_increments = 0
            current_points = playerParam.DG_Point / 10000
            playerUI.drive.points_changed, points_increments = imgui.slider_float(playerLabel .. " Drive Points (stock increments of 10%)", current_points, 0, 6, "%.1f")
            -- convert to points
            playerUI.drive.points = math.floor(points_increments * 10000)
        end
    end
    playerUI.drive.burnout_changed, playerUI.drive.burnout = imgui.checkbox(playerLabel .. " Burnout", playerParam.Is_DG_Break)
end

function module.ui.draw_super(playerUI, playerLabel, playerParam)
    -- Super UI elements would go here
    type_value = playerParam.SA_Type == 1
    playerUI.super.type_changed, playerUI.super.type_stock = imgui.checkbox(playerLabel .. " Super Stock", type_value)
    
    if playerUI.super.type_stock then 
        playerUI.super.super_stocks_changed, playerUI.super.super_stocks = imgui.slider_int(playerLabel .. " Super Stocks", playerParam.SA_Stock, 0, 3)
    else
        playerUI.super.type_points_enabled_changed, playerUI.super.type_points_enabled = imgui.checkbox(playerLabel .. " Enable pointwise super gauge adjustment", playerUI.super.type_points_enabled)
        
        if playerUI.super.type_points_enabled then
            playerUI.super.points_changed, playerUI.super.points = imgui.drag_int(playerLabel .. " Super Points", playerParam.SA_Point, 1, 0, 30000)
        else
            points_increments = 0
            current_points = playerParam.SA_Point / 10000
            playerUI.super.points_changed, points_increments = imgui.slider_float(playerLabel .. " Super Points (stock increments of 1%)", current_points, 0, 3, "%.2f")
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
        char_datas = { module.data.UniqueCharData[char_id1] }
    else 
        char_datas = { module.data.UniqueCharData[char_id1], module.data.UniqueCharData[char_id2] }
    end

    for _, char_data in pairs(char_datas) do
        if char_data then
            imgui.text("Character: " .. char_data.name)
            -- draw timers
            if char_data.timers then
                for _, timerData in pairs(char_data.timers) do
                    
                    local ui_timer = module.ui.unique[char_data.name][timerData.id]
                    descriptor = timerData.descriptors[ui_timer.timer_combo_value]
                    imgui.text(timerData.name .. ": " .. descriptor)

                    -- use stored slider value as current so it persists
                    ui_timer.timer_combo_changed, ui_timer.timer_combo_value = imgui.combo(timerData.name .. " Value ", ui_timer.timer_combo_value, timerData.descriptors)
                    if timerData.install == true and ui_timer.timer_combo_value == 2 then
                        -- installed timer UI elements
                        if ui_timer.installed_start_value == nil then ui_timer.installed_start_value = timerData.timerMaxValue end
                        ui_timer.installed_start_value_changed, ui_timer.installed_start_value = imgui.slider_int(timerData.name .. " starting activation value", ui_timer.installed_start_value, 0, timerData.timerMaxValue)
                    end
                end
            end

            -- draw stocks
            if char_data.stocks then
                for _, stockData in pairs(char_data.stocks) do
                    -- use stored stock ui values
                    local ui_stock = module.ui.unique[char_data.name][stockData.id]
                    descriptor = stockData.descriptors[ui_stock.stock_slider + 1]
                    imgui.text(stockData.name .. ": " .. descriptor)

                    if stockData.allowInfinite then
                        ui_stock.infinite_checkbox_changed, ui_stock.infinite_checkbox = imgui.checkbox("Toggle Infinite " .. stockData.name, ui_stock.infinite_checkbox)
                    end
                    -- if infinite is enabled, don't show the slider
                    if ui_stock.infinite_checkbox ~= true then
                        ui_stock.stock_slider_changed, ui_stock.stock_slider = imgui.slider_int(stockData.name .. " Value", ui_stock.stock_slider, stockData.minValue, stockData.maxValue)
                    end
                end
            end
        end
        imgui.separator()
    end
end

-- UI rendering function
function module.draw_ui()
    if imgui.collapsing_header("Training Parameters") then
        -- Health
        if imgui.tree_node("Health") then
            module.ui.p1.health.percentage_changed, module.ui.p1.health.percentage = imgui.slider_int("P1 Health Percentage", module.data.P1Param.Vital_Point, 0, 100)
            imgui.spacing()

            module.ui.p2.health.percentage_changed, module.ui.p2.health.percentage = imgui.slider_int("P2 Health Percentage", module.data.P2Param.Vital_Point, 0, 100)
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

    end
end

return module
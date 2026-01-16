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

function module.ui.init(playerUI, playerParam)

    -- init health ui data
    playerUI.health = {}
    -- set the health percentage to the player's vital point
    playerUI.health.percentage = playerParam.Vital_Point
    -- changed for percentage slider change
    playerUI.health.percentage_changed = false

    -- init drive ui data
    playerUI.drive = {}

    -- stock drive bars
    -- checks if the drive type is stock or points
    playerUI.drive.type_stock = true
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

    playerUI.super = {}
end

function module.ui.update(playerUI, playerParam)

    local need_update = false

    -- update health ui data
    if playerUI.health.percentage_changed then
        playerParam.Vital_Point = playerUI.health.percentage
        need_update = true
    end

    -- update drive ui data

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
        need_update = true
    end

    -- update drive points
    if playerUI.drive.points_changed then
        playerParam.DG_Point = playerUI.drive.points
        need_update = true
    end

    -- update burnout
    if playerUI.drive.burnout_changed then
        playerParam.Is_DG_Break = playerUI.drive.burnout
        need_update = true
    end


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

end

function module.on_frame()
    -- update live data references

    need_update = false

    need_update = need_update or module.ui.update(module.ui.p1, module.data.P1Param)
    need_update = need_update or module.ui.update(module.ui.p2, module.data.P2Param)

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
        playerUI.drive.type_points_enabled_changed, playerUI.drive.type_points_enabled = imgui.checkbox(playerLabel .. " Enable pointwise adjustment", playerUI.drive.type_points_enabled)
        
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
    end
end

return module
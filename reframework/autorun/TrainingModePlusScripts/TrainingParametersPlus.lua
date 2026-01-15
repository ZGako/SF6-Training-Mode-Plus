-- intellinsense
local re = re
local sdk = sdk
local reframework = reframework 
local imgui = imgui

local module = {}

-- temporary
local changed = false

module.name = "Training Parameters Plus"
module.description = "Module for adjusting training mode parameters. Modifiable parameters include: Health, Drive, Super, Position, Distance, Unique Resources"

module.data = {}
module.ui = {
    p1 = {
        health = {},
        drive = {}
    },
    p2 = {
        health = {},
        drive = {}
    },
}

module.health_presets = {
    refill = {
        Is_Vital_Recovery_Timer = true,
        Is_Vital_Infinity = false,
        Is_Vital_No_Recovery = false,
        Is_KO = false,
        Is_Point_Lock = true
    },
    fixed = {
        Is_Vital_Recovery_Timer = false,
        Is_Vital_Infinity = true,
        Is_Vital_No_Recovery = true,
        Is_KO = false,
        Is_Point_Lock = true
    },
    standard = {
        Is_Vital_Recovery_Timer = false,
        Is_Vital_Infinity = false,
        Is_Vital_No_Recovery = true,
        Is_KO = true,
        Is_Point_Lock = false
    }
}

module.drive_presets = {
    stock = {
        DG_Type = 1,
        Is_DG_Point_Lock = false,
    },
    point = {
        DG_Type = 3,
        Is_DG_Point_Lock = true,
    }
}

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

    -- *** Important fields I need from each ***


    -- TrainingManager._tfFuncs._entries[6]:get_field("value")
    -- sdk.call_object_func(param, "bApply") - this is for applying the changes immediately

    -- initialize UI variables
    -- p1

    --HP
    module.ui.p1.health.health_per = module.data.P1Param.Vital_Point
    module.ui.p1.health.health_per_changed = false
    module.ui.p1.health.health_points = math.ceil(module.ui.p1.health.health_per / 100 * module.data.live_P1.vital_max)
    module.ui.p1.health.health_points_changed = false
    module.ui.p1.health.checkbox_value = true
    module.ui.p1.health.checkbox_changed = false
    module.ui.p1.health.queue_hp_update = false
    -- set the preset for storage and restoring later
    if module.data.P1Param.Is_Vital_Recovery_Timer == true then
        module.ui.p1.health.old_preset = module.health_presets.refill
    elseif module.data.P1Param.Is_Vital_Infinity == true then
        module.ui.p1.health.old_preset = module.health_presets.fixed
    elseif module.data.P1Param.Is_KO == true then
        module.ui.p1.health.old_preset = module.health_presets.standard
    end
    -- DRIVE
    module.ui.p1.drive.drive_points = module.data.P1Param.DG_Point
    module.ui.p1.drive.drive_points_changed = false
    module.ui.p1.drive.drive_stocks = module.data.P1Param.DG_Stock
    module.ui.p1.drive.drive_stocks_changed = false
    module.ui.p1.drive.drive_burnout = module.data.P1Param.Is_DG_Break
    module.ui.p1.drive.drive_burnout_changed = false
    module.ui.p1.drive.checkbox_value = true
    module.ui.p1.drive.checkbox_changed = false



    -- p2

    -- HP
    module.ui.p2.health.health_per = module.data.P2Param.Vital_Point
    module.ui.p2.health.health_per_changed = false
    module.ui.p2.health.health_points = math.ceil(module.ui.p2.health.health_per / 100 * module.data.live_P2.vital_max)
    module.ui.p2.health.health_points_changed = false
    module.ui.p2.health.checkbox_value = true
    module.ui.p2.health.checkbox_changed = false
    module.ui.p2.health.queue_hp_update = false
    -- set the preset for storage and restoring later
    if module.data.P2Param.Is_Vital_Recovery_Timer == true then
        module.ui.p2.health.old_preset = module.health_presets.refill
    elseif module.data.P2Param.Is_Vital_Infinity == true then
        module.ui.p2.health.old_preset = module.health_presets.fixed
    elseif module.data.P2Param.Is_KO == true then
        module.ui.p2.health.old_preset = module.health_presets.standard
    end

    -- DRIVE
    module.ui.p2.drive.drive_points = module.data.P2Param.DG_Point
    module.ui.p2.drive.drive_points_changed = false
    module.ui.p2.drive.drive_stocks = module.data.P2Param.DG_Stock
    module.ui.p2.drive.drive_stocks_changed = false
    module.ui.p2.drive.drive_burnout = module.data.P2Param.Is_DG_Break
    module.ui.p2.drive.drive_burnout_changed = false
    module.ui.p2.drive.checkbox_value = true
    module.ui.p2.drive.checkbox_changed = false

    

end

function module.on_frame()
    
    -- variable indicating a request to call bApply at the end of the function
    local need_update = false

    -- if the percentage values change, update the training setting and apply it
    if module.ui.p1.health.health_per_changed == true then
        module.data.P1Param.Vital_Point = module.ui.p1.health.health_per

        -- update the points to keep them coupled
        if module.ui.p1.health.health_per ~= 0 then
            module.ui.p1.health.health_points = math.ceil(module.ui.p1.health.health_per / 100 * module.data.live_P1.vital_max)
        else 
            module.ui.p1.health.health_points = 1
        end

        need_update = true
    end

    if module.ui.p2.health.health_per_changed == true then
        module.data.P2Param.Vital_Point = module.ui.p2.health.health_per

        -- update the points to keep them coupled
        if module.ui.p2.health.health_per ~= 0 then
            module.ui.p2.health.health_points = math.ceil(module.ui.p2.health.health_per / 100 * module.data.live_P2.vital_max)
        else 
            module.ui.p2.health.health_points = 1
        end

        need_update = true
    end

    -- if health point value changes, update the live value, and set the stuff for proper calculation on refresh
    if module.ui.p1.health.health_points_changed == true then
        -- set the coupled health percentage value
        module.ui.p1.health.health_per = math.floor(module.ui.p1.health.health_points / module.data.live_P1.vital_max * 100)
        module.data.P1Param.Vital_Point = module.ui.p1.health.health_per

        module.data.live_P1.heal_new = module.ui.p1.health.health_points
        module.data.live_P1.vital_new = module.ui.p1.health.health_points

        -- change the recovery option to standard
        module.data.P1Param.Is_Vital_Recovery_Timer = module.health_presets.standard.Is_Vital_Recovery_Timer
        module.data.P1Param.Is_Vital_Infinity = module.health_presets.standard.Is_Vital_Infinity
        module.data.P1Param.Is_Vital_No_Recovery = module.health_presets.standard.Is_Vital_No_Recovery
        module.data.P1Param.Is_KO = module.health_presets.standard.Is_KO
        module.data.P1Param.Is_Point_Lock = module.health_presets.standard.Is_Point_Lock

        need_update = true

    end

    if module.ui.p2.health.health_points_changed == true then
        -- set the coupled health percentage value
        module.ui.p2.health.health_per = math.floor(module.ui.p2.health.health_points / module.data.live_P2.vital_max * 100)
        module.data.P2Param.Vital_Point = module.ui.p2.health.health_per

        module.data.live_P2.heal_new = module.ui.p2.health.health_points
        module.data.live_P2.vital_new = module.ui.p2.health.health_points

        -- change the recovery option to standard
        module.data.P2Param.Is_Vital_Recovery_Timer = module.health_presets.standard.Is_Vital_Recovery_Timer
        module.data.P2Param.Is_Vital_Infinity = module.health_presets.standard.Is_Vital_Infinity
        module.data.P2Param.Is_Vital_No_Recovery = module.health_presets.standard.Is_Vital_No_Recovery
        module.data.P2Param.Is_KO = module.health_presets.standard.Is_KO
        module.data.P2Param.Is_Point_Lock = module.health_presets.standard.Is_Point_Lock

        need_update = true

    end

    -- if the checkbox changes and is enabled (which means it's back to percentage setting), restore the previous value
    if module.ui.p1.health.checkbox_changed == true then
        if module.ui.p1.health.checkbox_value == true then
            -- restore the recovery option to the old one
            module.data.P1Param.Is_Vital_Recovery_Timer = module.ui.p1.health.old_preset.Is_Vital_Recovery_Timer
            module.data.P1Param.Is_Vital_Infinity = module.ui.p1.health.old_preset.Is_Vital_Infinity
            module.data.P1Param.Is_Vital_No_Recovery = module.ui.p1.health.old_preset.Is_Vital_No_Recovery
            module.data.P1Param.Is_KO = module.ui.p1.health.old_preset.Is_KO
            module.data.P1Param.Is_Point_Lock = module.ui.p1.health.old_preset.Is_Point_Lock
        else
            -- set the preset for storage and restoring later
            if module.data.P1Param.Is_Vital_Recovery_Timer == true then
                module.ui.p1.health.old_preset = module.health_presets.refill
            elseif module.data.P1Param.Is_Vital_Infinity == true then
                module.ui.p1.health.old_preset = module.health_presets.fixed
            elseif module.data.P1Param.Is_KO == true then
                module.ui.p1.health.old_preset = module.health_presets.standard
            end
        end

        need_update = true
    end

    if module.ui.p2.health.checkbox_changed == true and module.ui.p2.health.checkbox_value == true then
        if module.ui.p2.health.checkbox_value == true then
            -- restore the recovery option to the old one
            module.data.P2Param.Is_Vital_Recovery_Timer = module.ui.p2.health.old_preset.Is_Vital_Recovery_Timer
            module.data.P2Param.Is_Vital_Infinity = module.ui.p2.health.old_preset.Is_Vital_Infinity
            module.data.P2Param.Is_Vital_No_Recovery = module.ui.p2.health.old_preset.Is_Vital_No_Recovery
            module.data.P2Param.Is_KO = module.ui.p2.health.old_preset.Is_KO
            module.data.P2Param.Is_Point_Lock = module.ui.p2.health.old_preset.Is_Point_Lock
        else
            -- set the preset for storage and restoring later
            if module.data.P2Param.Is_Vital_Recovery_Timer == true then
                module.ui.p2.health.old_preset = module.health_presets.refill
            elseif module.data.P2Param.Is_Vital_Infinity == true then
                module.ui.p2.health.old_preset = module.health_presets.fixed
            elseif module.data.P2Param.Is_KO == true then
                module.ui.p2.health.old_preset = module.health_presets.standard
            end
        end

        need_update = true
    end

    -- update the health value after the refresh happens (prolly doesn't work but whatever)
    if module.ui.p1.health.queue_hp_update == true and module.data.TrainingManager._TrainingState == 1 then
        module.ui.p1.health.queue_hp_update = false
        module.data.live_P1.heal_new = module.ui.p1.health.health_points
        module.data.live_P1.vital_new = module.ui.p1.health.health_points
    end

    if module.ui.p2.health.queue_hp_update == true and module.data.TrainingManager._TrainingState == 2 then
        module.ui.p2.health.queue_hp_update = false
        module.data.live_P2.heal_new = module.ui.p2.health.health_points
        module.data.live_P2.vital_new = module.ui.p2.health.health_points
    end

    -- DRIVE UPDATES
    if module.ui.p1.drive.checkbox_changed == true then
        if module.ui.p1.drive.checkbox_value == true then
            -- switch to stocks
            module.data.P1Param.DG_Type = module.drive_presets.stock.DG_Type
            module.data.P1Param.Is_DG_Point_Lock = module.drive_presets.stock.Is_DG_Point_Lock
            module.data.P1Param.DG_Stock = module.ui.p1.drive.drive_stocks
        else
            -- switch to points
            module.data.P1Param.DG_Type = module.drive_presets.point.DG_Type
            module.data.P1Param.Is_DG_Point_Lock = module.drive_presets.point.Is_DG_Point_Lock
            module.data.P1Param.DG_Point = module.ui.p1.drive.drive_points
        end
        need_update = true
    end

    if module.ui.p1.drive.drive_points_changed == true then
        module.data.P1Param.DG_Point = module.ui.p1.drive.drive_points

        -- update the stocks to keep them coupled
        if module.ui.p1.drive.drive_points ~= 0 then
            module.ui.p1.drive.drive_stocks = math.ceil(module.ui.p1.drive.drive_points / 10000)
        else
            module.ui.p1.drive.drive_stocks = 0
        end

        need_update = true
    end

    if module.ui.p1.drive.drive_stocks_changed == true then
        module.data.P1Param.DG_Stock = module.ui.p1.drive.drive_stocks

        -- update the points to keep them coupled
        module.ui.p1.drive.drive_points = module.ui.p1.drive.drive_stocks * 10000
        need_update = true
    end

    if module.ui.p1.drive.drive_burnout_changed == true then
        module.data.P1Param.Is_DG_Break = module.ui.p1.drive.drive_burnout
        need_update = true
    end

    -- P2 DRIVE UPDATES
    if module.ui.p2.drive.checkbox_changed == true then
        if module.ui.p2.drive.checkbox_value == true then
            -- switch to stocks
            module.data.P2Param.DG_Type = module.drive_presets.stock.DG_Type
            module.data.P2Param.Is_DG_Point_Lock = module.drive_presets.stock.Is_DG_Point_Lock
            module.data.P2Param.DG_Stock = module.ui.p2.drive.drive_stocks
        else
            -- switch to points
            module.data.P2Param.DG_Type = module.drive_presets.point.DG_Type
            module.data.P2Param.Is_DG_Point_Lock = module.drive_presets.point.Is_DG_Point_Lock
            module.data.P2Param.DG_Point = module.ui.p2.drive.drive_points
        end
        need_update = true
    end

    if module.ui.p2.drive.drive_points_changed == true then
        module.data.P2Param.DG_Point = module.ui.p2.drive.drive_points
        -- update the stocks to keep them coupled
        if module.ui.p2.drive.drive_points ~= 0 then
            module.ui.p2.drive.drive_stocks = math.ceil(module.ui.p2.drive.drive_points / 10000)
        else
            module.ui.p2.drive.drive_stocks = 0
        end
        need_update = true
    end

    if module.ui.p2.drive.drive_stocks_changed == true then
        module.data.P2Param.DG_Stock = module.ui.p2.drive.drive_stocks
        -- update the points to keep them coupled
        module.ui.p2.drive.drive_points = module.ui.p2.drive.drive_stocks * 10000
        need_update = true
    end

    if module.ui.p2.drive.drive_burnout_changed == true then
        module.data.P2Param.Is_DG_Break = module.ui.p2.drive.drive_burnout
        need_update = true
    end

    -- is request refresh?
    if module.data.TrainingManager._IsReqRefresh then
        -- if we're using health points
        if not module.ui.p1.health.checkbox_value then
            module.ui.p1.health.queue_hp_update = true
        end

        if not module.ui.p2.health.checkbox_value then
            module.ui.p2.health.queue_hp_update = true
        end
    end

    -- updates the training mode immediately rather than waiting for refresh or whatever
    if need_update then
        sdk.call_object_func(module.data.tf_PS, "bApply")
    end

end

function module.draw_ui()

    if imgui.collapsing_header("Training Parameters Plus") then
        -- ALL STILL TODO, JUST A VISUAL PREVIEW
        if imgui.tree_node("Health") then

            if module.ui.p1.health.checkbox_value then
                module.ui.p1.health.health_per_changed, module.ui.p1.health.health_per = imgui.slider_int("P1 Health Percentage", module.ui.p1.health.health_per, 0, 100)
            else
                module.ui.p1.health.health_points_changed, module.ui.p1.health.health_points = imgui.drag_int("P1 Health Points", module.ui.p1.health.health_points, 10, 1, module.data.live_P1.vital_max)
            end
            module.ui.p1.health.checkbox_changed, module.ui.p1.health.checkbox_value = imgui.checkbox("Toggle P1 Health Percentage", module.ui.p1.health.checkbox_value)
            
            imgui.spacing()

            if module.ui.p2.health.checkbox_value then
                module.ui.p2.health.health_per_changed, module.ui.p2.health.health_per = imgui.slider_int("P2 Health Percentage", module.ui.p2.health.health_per, 0, 100)
            else
                module.ui.p2.health.health_points_changed, module.ui.p2.health.health_points = imgui.drag_int("P2 Health Points", module.ui.p2.health.health_points, 10, 1, module.data.live_P2.vital_max)
            end
            module.ui.p2.health.checkbox_changed, module.ui.p2.health.checkbox_value = imgui.checkbox("Toggle P2 Health Percentage", module.ui.p2.health.checkbox_value)
            
            if not (module.ui.p1.health.checkbox_value and module.ui.p2.health.checkbox_value) then
                imgui.text_colored("Warning: Changing the Health Points directly will switch the health recovery mode to 'Standard'\nThe feature also may or may not work well", 0xFF00A9F9)
            end

            imgui.tree_pop()
        end

        -- DRIVE UI
        if imgui.tree_node("Drive") then

            if module.ui.p1.drive.checkbox_value then
                module.ui.p1.drive.drive_stocks_changed, module.ui.p1.drive.drive_stocks = imgui.slider_int("P1 Drive Stocks", module.ui.p1.drive.drive_stocks, 0, 6)
            else
                module.ui.p1.drive.drive_points_changed, module.ui.p1.drive.drive_points = imgui.drag_int("P1 Drive Points", module.ui.p1.drive.drive_points, 1, 0, 60000)
            end

            module.ui.p1.drive.checkbox_changed, module.ui.p1.drive.checkbox_value = imgui.checkbox("Toggle P1 Drive Stocks/Points", module.ui.p1.drive.checkbox_value)

            module.ui.p1.drive.drive_burnout_changed, module.ui.p1.drive.drive_burnout = imgui.checkbox("P1 Burnout", module.data.P1Param.Is_DG_Break)

            imgui.spacing()

            if module.ui.p2.drive.checkbox_value then
                module.ui.p2.drive.drive_stocks_changed, module.ui.p2.drive.drive_stocks = imgui.slider_int("P2 Drive Stocks", module.ui.p2.drive.drive_stocks, 0, 6)
            else
                module.ui.p2.drive.drive_points_changed, module.ui.p2.drive.drive_points = imgui.drag_int("P2 Drive Points", module.ui.p2.drive.drive_points, 1, 0, 60000)
            end
            module.ui.p2.drive.checkbox_changed, module.ui.p2.drive.checkbox_value = imgui.checkbox("Toggle P2 Drive Stocks/Points", module.ui.p2.drive.checkbox_value)
            module.ui.p2.drive.drive_burnout_changed, module.ui.p2.drive.drive_burnout = imgui.checkbox("P2 Burnout", module.data.P2Param.Is_DG_Break)

            imgui.tree_pop()
        end

    end
end

return module
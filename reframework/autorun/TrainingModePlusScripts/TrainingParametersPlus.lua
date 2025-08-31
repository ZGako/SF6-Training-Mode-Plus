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
        health = {}
    },
    p2 = {
        health = {}
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

    -- module.ui.p1.health_per = module.data.P1Param.Vital_Point
    -- module.ui.p2.health_per = module.data.P2Param.Vital_Point
    -- module.ui.p1.health_unit = math.ceil(module.ui.p1.health_per / 100 * module.ui.p1.max_health)
    -- module.ui.p2.health_unit = math.ceil(module.ui.p2.health_per / 100 * module.ui.p2.max_health)
    -- module.ui.health_changed = false
    -- module.ui.advanced_health_toggle = false

    -- health percentage, health units, advanced_health_toggle, health_per_changed (both p1 p2), health_unit_changed
    -- For both P1 and P2:
    -- health percentage, health_checkbox, health slider change, checkbox change?

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

    -- update the health value after the refresh happens
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
                imgui.text_colored("Warning: Changing the Health Points directly will switch the health recovery mode to 'Standard'", 0xFF00A9F9)
            end

            imgui.tree_pop()
        end

    end
end

return module
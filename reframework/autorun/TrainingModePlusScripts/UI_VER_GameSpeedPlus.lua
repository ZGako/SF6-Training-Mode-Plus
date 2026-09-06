-- intellinsense
local re = re
local sdk = sdk
local reframework = reframework
local imgui = imgui

local module = {}

module.name = "Game Speed Plus"
module.description = "Module for adjusting game speed beyond the normal settings provided by default"

module.data = {}
module.ui = {}

function module.init()
    -- get the important fields at init time
    local TrainingManager = sdk.get_managed_singleton("app.training.TrainingManager")
    local TrainingData = TrainingManager:get_field("_tData")

    module.data.TMF = sdk.find_type_definition("app.training.TrainingMenuFunc")

    module.data.OtherSetting = TrainingData:get_field("OtherSetting")
    module.data.tf_OS = TrainingManager._tfFuncs._entries[10]:get_field("value")

    local function replace_output_hook(name, pre)
        local m = module.data.TMF:get_method(name)
        if not m then
            return
        end
        sdk.hook(
            m,
            function(args)
                local st = thread.get_hook_storage()
                if pre(args, st) then
                    return sdk.PreHookResult.SKIP_ORIGINAL
                end
            end,
            function(rv)
                local st = thread.get_hook_storage()
                if st.str then
                    return sdk.to_ptr(sdk.create_managed_string(st.str))
                end
                if st.ret ~= nil then
                    return sdk.to_ptr(st.ret)
                end
                return rv
            end
        )
    end

    -- setup the variables for the ingame UI mod

    local function apply_custom_speed(speed)
        -- module.data.OtherSetting.Is_Speed_Setting = true
        -- module.data.tf_OS._GameData.IsMenuPause = false
        -- module.data.OtherSetting.OS_Game_Speed = speed

        -- use game's functions in case something else wants to hook into them or they do more than just set the values
        sdk.call_object_func(module.data.tf_OS.FuncList, "SetActiveGameSpeed(System.Boolean)", true)
        sdk.call_object_func(module.data.tf_OS.FuncList, "SetMenuPause(System.Boolean)", false)
        sdk.call_object_func(module.data.tf_OS.FuncList, "SetGameSpeed(app.training.GameSpeed)", speed)
    end

    local function get_current_speed_index()
        -- if Is_speed_setting is false, the game speed is 1.0 (index 1)
        if module.data.tf_OS._GameData.IsMenuPause then
            return 11
        end
        if not module.data.OtherSetting.Is_Speed_Setting then
            return 0
        end
        local current_speed = module.data.OtherSetting.OS_Game_Speed + 1
        if current_speed >= 6 then
            return current_speed - 1
        else
            return current_speed
        end
    end

    local function_lookup_table = {
        [347] = {
            name = "GAME_SPEED_60",
            execute = function()
                apply_custom_speed(1)
            end
        },
        [348] = {
            name = "GAME_SPEED_70",
            execute = function()
                apply_custom_speed(2)
            end
        },
        [349] = {
            name = "GAME_SPEED_80",
            execute = function()
                apply_custom_speed(3)
            end
        },
        [350] = {
            name = "GAME_SPEED_90",
            execute = function()
                apply_custom_speed(4)
            end
        },
        [351] = {
            name = "GAME_SPEED_110",
            execute = function()
                apply_custom_speed(6)
            end
        },
        [352] = {
            name = "GAME_SPEED_120",
            execute = function()
                apply_custom_speed(7)
            end
        },
        [353] = {
            name = "GAME_SPEED_130",
            execute = function()
                apply_custom_speed(8)
            end
        },
        [354] = {
            name = "GAME_SPEED_140",
            execute = function()
                apply_custom_speed(9)
            end
        },
        [355] = {
            name = "GAME_SPEED_150",
            execute = function()
                apply_custom_speed(10)
            end
        }
    }

    -- Setup Hooks
    sdk.hook(
        sdk.find_type_definition("app.training.UIFlowTrainingMenu.Param"):get_method("InitSpinBox(System.Int32)"),
        function(args)
            -- get args
            local current_object = sdk.to_managed_object(args[2])
            local current_index = sdk.to_int64(args[3]) & 0xFFFFFFFF

            local current_TMD = current_object:get_CurrentParentData()
            if current_TMD == nil then
                return
            end

            -- parentdata functiontype determines the TAB we're on
            local currentTabFuncType = current_TMD._FuncType

            -- technically as long as we apply the changes at the 0 index, they get propagated throughout all the spinboxes (as the game doesn't update the requests beforehand)
            if currentTabFuncType ~= 2 or current_index ~= 3 then
                return
            end

            -- actually apply the behavior here
            local viewdatalist = current_object._ViewDataList

            viewdatalist[3].Index = get_current_speed_index()
        end,
        function(retval)
            return retval
        end
    )

    replace_output_hook(
        "Function(app.training.TrainingFuncType, app.training.BaseParam, app.training.UIFlowTrainingMenu.Param.ViewData, System.Int32)",
        function(args, st)
            local funcType = sdk.to_int64(args[3]) & 0xFFFFFFFF

            local action = function_lookup_table[funcType]
            if action then
                action.execute()
                st.ret = true
                return true
            end
        end
    )
end

function module.on_frame()
end

function module.draw_ui()
end

return module

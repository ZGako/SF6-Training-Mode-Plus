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
    module.data.OtherSetting = TrainingData:get_field("OtherSetting")
    module.data.tf_OS = TrainingManager._tfFuncs._entries[10]:get_field("value")

    -- *** Important fields I need from each ***
    -- OtherSetting -> OS_Game_Speed - game speed enum (0 to 10)
    -- OtherSetting -> Is_Speed_Setting - boolean for enabling different speeds
    -- tf_OS -> ApplyGameSpeed() - function to apply the game speed

    -- *** Init UI data variables ***

    -- this variable exists to deal with someone using the ingame menu and this one at the same time
    module.ui.speed_changed_by_script = false

    if module.data.OtherSetting.Is_Speed_Setting then
        module.ui.speed = 1 -- game default of 50%
    else
        module.ui.speed = 6 -- default to 100% speed if not changed
    end

    -- Setup Hooks

    sdk.hook(
        sdk.find_type_definition("app.training.tf_OtherSetting.FuncData"):get_method(
            "SetGameSpeed(app.training.GameSpeed)"
        ),
        function(args)
            local ingame_new_speed = sdk.to_int64(args[3])
            -- if the new speed is not 100%
            if ingame_new_speed ~= 5 and module.ui.speed ~= 6 then
                return sdk.PreHookResult.SKIP_ORIGINAL
            else
                module.ui.speed = ingame_new_speed + 1
            end
        end
    )
end

function module.on_frame()
    if module.ui.speed_changed_by_script then
        module.ui.speed_changed_by_script = false

        -- if the game is 100%, disable the boolean (that way it doesn't mess as much with the ingame UI)
        if module.ui.speed ~= 6 then
            module.data.OtherSetting.Is_Speed_Setting = true
        else
            module.data.OtherSetting.Is_Speed_Setting = false
        end

        -- set the game speed based on the UI value
        module.data.OtherSetting.OS_Game_Speed = module.ui.speed - 1

        sdk.call_object_func(module.data.tf_OS, "ApplyGameSpeed")
    end
end

function module.draw_ui()
    if imgui.collapsing_header("Game Speed Plus") then
        module.ui.speed_changed_by_script, module.ui.speed =
            imgui.combo(
            "Game Speed",
            module.ui.speed,
            {"50%", "60%", "70%", "80%", "90%", "100%", "110%", "120%", "130%", "140%", "150%"}
        )

        imgui.same_line()

        if imgui.button("Reset to Standard") then
            module.ui.speed_changed_by_script = true
            module.ui.speed = 6
        end

        imgui.text("Note: You can use both the script menu and the ingame menu. Latest change takes precedence.")
    end
end

return module

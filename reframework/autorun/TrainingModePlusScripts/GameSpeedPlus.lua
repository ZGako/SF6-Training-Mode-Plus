local module = {}

module.name = "Game Speed Plus"
module.description = "Module for adjusting game speed beyond the normal settings provided by default"

module.data = {}
module.ui = {}

function module.init()

    -- get the important fields at init time
    local TrainingManager = sdk.get_managed_singleton("app.training.TrainingManager")
    module.data.TrainingData = TrainingManager:get_field("_tData")
    module.data.OtherSetting = module.data.TrainingData:get_field("OtherSetting")

    -- *** Important fields I need from each ***
    -- TrainingData -> _IsReqRefresh - needed to check for refresh
    -- OtherSetting -> OS_Game_Speed - game speed enum (0 to 10)
    -- OtherSetting -> Is_Speed_Setting - boolean for enabling different speeds
    -- OtherSetting -> ApplyGameSpeed()
    -- trainingmanager -> _tfFuncs -> _entries[10].value = tf_OtherSetting -> call "ApplyGameSpeed()"
    -- training._tfFuncs._entries[10].value
    -- tf_OtherSetting.FuncData -> call "SetGameSpeed"

    -- Init UI data variables

    -- this variable exists to deal with someone using the ingame menu and this one at the same time
    module.ui.changed_speed = false

    if module.data.OtherSetting.Is_Speed_Setting then
        module.ui.speed = 1 -- game default of 50%
    else
        module.ui.speed = 6 -- default to 100% speed if not changed
    end

end

function module.on_frame()

    if module.ui.changed_speed then
        re.msg("Speed selected: " .. module.ui.speed)
        if module.ui.speed ~= 6 then
            module.data.OtherSetting.OS_Game_Speed = module.ui.speed - 1
            module.data.OtherSetting.Is_Speed_Setting = true
        else
            module.data.OtherSetting.Is_Speed_Setting = false
        end
        module.ui.changed_speed = false
    end

    if module.data.TrainingData._IsReqRefresh then

        -- update UI if using the ingame menu
        if module.data.OtherSetting.Is_Speed_Setting then
            module.ui.speed = module.data.OtherSetting.OS_Game_Speed + 1
        else
            module.ui.speed = 6 -- game default of 100%
        end
    end
end

function module.draw_ui()

    if imgui.collapsing_header("Game Speed Plus") then

        module.ui.changed_speed, module.ui.speed = imgui.combo("Game Speed", module.ui.speed,
        {"50%", "60%", "70%", "80%", "90%", "100%", "110%", "120%", "130%", "140%", "150%"})

        imgui.text("Note: Remember to refresh the training mode to apply the new settings.")
        imgui.text("Note: You can reset to standard speed both through the ingame menu and the script")
        imgui.text("Note: Try avoiding changing these settings with the ingame menu open on the 'Environment' tab.")

    end

end

return module
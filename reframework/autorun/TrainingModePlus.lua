-- Training Mode Plus - TMPlus

-- intellinsense
local re = re
local sdk = sdk
local reframework = reframework 
local imgui = imgui

-- getting the TrainingManager singleton also helps for determining SF6 initialization
local TrainingManager = nil
local TrainingStateChange = false
local ShowScriptUI = true

-- require modules here
local tmplus_modules = {
    require("TrainingModePlusScripts/GameSpeedPlus")
}

re.on_frame(function ()
    if not TrainingManager then
        -- objective here is to get the singletons to check training status
        TrainingManager = sdk.get_managed_singleton("app.training.TrainingManager")
        if not TrainingManager then
            -- return here to not execute the rest of the on_frame function
            -- not needed but I'd rather not have indentation on the function body
            return
        end
    end

    -- if we reach this point then we have the training manager
    -- we can now check if we are in training mode
    if TrainingManager._TrainingState ~= 0 then 

        -- look if the state just changed from not being in training mode to reaching training mode
        if not TrainingStateChange then
            TrainingStateChange = true

            -- module data initialization
            for _, module in ipairs(tmplus_modules) do
                module.init()
            end
            
        end

        -- modules on frame calls
        for _, module in ipairs(tmplus_modules) do
            module.on_frame()
        end


        if ShowScriptUI and reframework:is_drawing_ui() then
            if imgui.begin_window("Training Mode Plus", true, 0) then

                imgui.spacing()

                if imgui.button("Refresh Training Mode") then
                    TrainingManager._IsReqRefresh = true
                end

                -- modules UI
                for _, module in ipairs(tmplus_modules) do
                    module.draw_ui()
                    imgui.spacing()
                end

                imgui.end_window()
            else
                ShowScriptUI = false
            end
        end

    else

        if TrainingStateChange then
            TrainingStateChange = false
        end

    end

end)

re.on_draw_ui(function ()
    -- draw basic UI within the reframework console
    if imgui.tree_node("Training Mode Plus") then
        if imgui.button(ShowScriptUI and "Hide Script UI" or "Show Script UI") then
            ShowScriptUI = not ShowScriptUI
        end

        if imgui.tree_node("Modules loaded") then
            for _, module in ipairs(tmplus_modules) do
                imgui.text_colored(module.name, 0xFFAAFFFF)
                imgui.same_line()
                imgui.text(module.description)
            end
            imgui.tree_pop()
        end

        imgui.tree_pop()
    end

end)
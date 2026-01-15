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
-- Safely require modules so a broken module won't crash the entire script.
local tmplus_modules = {}
local function safe_require(path)
    local ok, mod_or_err = pcall(require, path)
    if not ok then
        print(string.format("[TrainingModePlus] require('%s') failed: %s", path, tostring(mod_or_err)))
        return nil
    end
    if type(mod_or_err) ~= "table" then
        print(string.format("[TrainingModePlus] module '%s' did not return a table (got %s)", path, type(mod_or_err)))
        return nil
    end
    return mod_or_err
end

do
    local mod = safe_require("TrainingModePlusScripts/GameSpeedPlus")
    if mod then table.insert(tmplus_modules, mod) end
    local mod2 = safe_require("TrainingModePlusScripts/TrainingParametersPlus")
    if mod2 then table.insert(tmplus_modules, mod2) end
end

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

            -- module data initialization (guard nil functions and log helpful messages)
            for _, module in ipairs(tmplus_modules) do
                if module == nil then
                    print("[TrainingModePlus] encountered nil module in tmplus_modules")
                else
                    if type(module.init) == "function" then
                        module.init()
                    else
                        print(string.format("[TrainingModePlus] module '%s' has no init() function", tostring(module.name)))
                    end
                end
            end
            
        end

        -- modules on frame calls (guard nil functions)
        for _, module in ipairs(tmplus_modules) do
            if module == nil then
                -- skip
            else
                if type(module.on_frame) == "function" then
                    module.on_frame()
                else
                    -- Only warn once to avoid spamming; print minimal info
                    -- This print will help identify which module lost its on_frame
                    print(string.format("[TrainingModePlus] module '%s' has no on_frame() function", tostring(module.name)))
                end
            end
        end


        if ShowScriptUI and reframework:is_drawing_ui() then
            if imgui.begin_window("Training Mode Plus", true, 0) then

                imgui.spacing()

                if imgui.button("Refresh Training Mode") then
                    TrainingManager._IsReqRefresh = true
                end

                -- modules UI (guard nil functions)
                for _, module in ipairs(tmplus_modules) do
                    if module and type(module.draw_ui) == "function" then
                        module.draw_ui()
                    else
                        if module then
                            print(string.format("[TrainingModePlus] module '%s' has no draw_ui() function", tostring(module.name)))
                        end
                    end
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
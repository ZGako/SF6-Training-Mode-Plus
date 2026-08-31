-- intellinsense
local re = re
local sdk = sdk
local reframework = reframework
local imgui = imgui
local json = json

local module = {
    data = {},
    ui_active = false,
    request_refresh = false,
    request_randomizer = false,
    -- this is the default value, once you override it the config will save the new value
    hotkeys = {
        ["request_randomizer"] = "Select",
        ["request_randomizer_$"] = "L3"
    }
}

local Hotkey = require("Hotkeys/Hotkeys")

module.name = "Training Settings + Randomizer"
module.description =
    "Module for adjusting training mode settings and randomizing them. Modifiable parameters include: Health, Drive, Super, Position, Unique Resources"

--[[
    We use the MVC pattern to separate data, logic, and UI.
    The module is what ties all the separate MVC compoenents together.

    The model contains the live game data.
    The controller has variables for logic and states that get saved to memory to reload during different insteances of training sessions.
    The view has variables for UI that can be freely discarted between sessions.
]]
--[[
    player specific parameters (grouped into one since they have very similar and simple logic)
]]
local PlayerParam = {
    model = {
        p1 = {},
        p2 = {}
    },
    view = {
        p1 = {},
        p2 = {}
    },
    controller = {
        p1 = {},
        p2 = {}
    }
}

function PlayerParam:init_player(PlayerIndex, PlayerParams)
    local PlayerView = self.view[PlayerIndex]
    local PlayerController = self.controller[PlayerIndex]

    -- copy the model
    self.model[PlayerIndex] = PlayerParams
    PlayerParams.Is_DG_Point_Lock = true

    --[[
        Health parameter initialization
    ]]
    -- define the controller variables
    PlayerController.health_randomizer = {}
    PlayerController.health_randomizer.enabled = false -- health randomizer enabled flag
    PlayerController.health_randomizer.bounds_enabled = false -- health randomizer bounds enabled flag
    PlayerController.health_randomizer.lower_bound = 0 -- health randomizer lower bound
    PlayerController.health_randomizer.upper_bound = 100 -- health randomizer upper bound

    -- define the view variables
    PlayerView.health = PlayerParams.Vital_Point -- current health value
    PlayerView.health_changed = false -- slider changed flag

    --[[ 
    Drive parameter initialization 
    ]]
    -- define the controller variables

    PlayerController.drive_randomizer = {}
    PlayerController.drive_randomizer.enabled = false -- drive randomizer enabled flag
    PlayerController.drive_randomizer.bounds_enabled = false -- drive randomizer bounds enabled flag
    PlayerController.drive_randomizer.lower_bound_stock = 0 -- drive randomizer lower bound
    PlayerController.drive_randomizer.upper_bound_stock = 6 -- drive randomizer upper bound
    PlayerController.drive_randomizer.lower_bound_points = 0 -- drive randomizer lower bound
    PlayerController.drive_randomizer.upper_bound_points = 60000 -- drive randomizer upper bound

    -- drive points type, true == absolute, false == percentage
    PlayerController.drive_points_type = false

    -- define the view variables
    PlayerView.burnout = PlayerParams.Is_DG_Break
    PlayerView.burnout_changed = false

    -- drive type, true == stock, false == custom
    PlayerView.drive_type = PlayerParams.DG_Type == 1
    PlayerView.drive_type_changed = false

    -- drive stocks
    PlayerView.drive_stocks = PlayerParams.DG_Stock
    PlayerView.drive_stocks_changed = false

    -- drive points
    PlayerView.drive_points = PlayerParams.DG_Point
    PlayerView.drive_points_changed = false

    --[[
        Super parameter initialization
    ]]
    PlayerController.super_randomizer = {}
    PlayerController.super_randomizer.enabled = false -- super randomizer enabled flag
    PlayerController.super_randomizer.bounds_enabled = false -- super randomizer bounds enabled flag
    PlayerController.super_randomizer.lower_bound_stock = 0 -- super randomizer lower bound
    PlayerController.super_randomizer.upper_bound_stock = 3 -- super randomizer upper bound
    PlayerController.super_randomizer.lower_bound_points = 0 -- super randomizer lower bound
    PlayerController.super_randomizer.upper_bound_points = 30000 -- super randomizer upper bound

    -- super points type
    PlayerController.super_points_type = false

    -- define the view variables

    -- super type, true == stock, false == custom
    PlayerView.super_type = PlayerParams.SA_Type == 1
    PlayerView.super_type_changed = false

    -- super stocks
    PlayerView.super_stocks = PlayerParams.SA_Stock
    PlayerView.super_stocks_changed = false

    -- super points
    PlayerView.super_points = PlayerParams.SA_Point
    PlayerView.super_points_changed = false
end

function PlayerParam:update_player_parameters(PlayerIndex)
    local PlayerModel = self.model[PlayerIndex]
    local PlayerView = self.view[PlayerIndex]
    local PlayerController = self.controller[PlayerIndex]

    local need_apply = false

    -- update health logic
    if not PlayerController.health_randomizer.enabled then
        -- if the randomizer is disabled, use the slider value
        if PlayerView.health_changed then
            PlayerModel.Vital_Point = PlayerView.health
            need_apply = true
        end
    end

    -- update drive logic

    -- drive type
    if PlayerView.drive_type_changed then
        if PlayerView.drive_type then
            -- stock type
            PlayerModel.DG_Type = 1
        else
            -- custom type
            PlayerModel.DG_Type = 3
            PlayerModel.Is_DG_Point_Lock = true
        end
        need_apply = true
    end

    -- burnout
    if PlayerView.burnout_changed then
        PlayerModel.Is_DG_Break = PlayerView.burnout
        need_apply = true
    end

    if not PlayerController.drive_randomizer.enabled then
        -- if the randomizer is disabled, use the slider value

        if PlayerView.drive_points_changed then
            -- custom type
            PlayerModel.DG_Point = PlayerView.drive_points
            PlayerModel.DG_Stock = math.floor((PlayerView.drive_points + 5000) / 10000)
            need_apply = true
        end

        if PlayerView.drive_stocks_changed then
            -- stock type
            PlayerModel.DG_Stock = PlayerView.drive_stocks
            PlayerModel.DG_Point = PlayerView.drive_stocks * 10000
            need_apply = true
        end
    end

    -- update super logic
    if PlayerView.super_type_changed then
        if PlayerView.super_type then
            -- stock type
            PlayerModel.SA_Type = 1
        else
            -- custom type
            PlayerModel.SA_Type = 3
            PlayerModel.Is_SA_Point_Lock = true
        end
        need_apply = true
    end

    if not PlayerController.super_randomizer.enabled then
        -- if the randomizer is disabled, use the slider value

        if PlayerView.super_points_changed then
            -- custom type
            PlayerModel.SA_Point = PlayerView.super_points
            PlayerModel.SA_Stock = math.floor((PlayerView.super_points + 5000) / 10000)
            need_apply = true
        end

        if PlayerView.super_stocks_changed then
            -- stock type
            PlayerModel.SA_Stock = PlayerView.super_stocks
            PlayerModel.SA_Point = PlayerView.super_stocks * 10000
            need_apply = true
        end
    end

    return need_apply
end

function PlayerParam:randomize_player_health(PlayerIndex)
    if not self.controller[PlayerIndex].health_randomizer.enabled then
        return
    end

    local PlayerModel = self.model[PlayerIndex]
    local PlayerView = self.view[PlayerIndex]
    local PlayerController = self.controller[PlayerIndex]

    -- randomize health logic
    local lower_bound = 0
    local upper_bound = 100
    if PlayerController.health_randomizer.bounds_enabled then
        lower_bound = PlayerController.health_randomizer.lower_bound
        upper_bound = PlayerController.health_randomizer.upper_bound
    end
    local randomized_health = math.random(lower_bound, upper_bound)
    PlayerModel.Vital_Point = randomized_health
end

function PlayerParam:randomize_player_drive(PlayerIndex)
    if not self.controller[PlayerIndex].drive_randomizer.enabled then
        return
    end

    local PlayerModel = self.model[PlayerIndex]
    local PlayerView = self.view[PlayerIndex]
    local PlayerController = self.controller[PlayerIndex]

    -- randomize drive logic
    if PlayerView.drive_type then
        -- stock type
        local lower_bound = -6
        local upper_bound = 6
        if PlayerController.drive_randomizer.bounds_enabled then
            lower_bound = PlayerController.drive_randomizer.lower_bound_stock
            upper_bound = PlayerController.drive_randomizer.upper_bound_stock
        end
        local randomized_stocks = math.random(lower_bound, upper_bound)
        if randomized_stocks < 0 then
            randomized_stocks = -randomized_stocks
            PlayerModel.Is_DG_Break = true
        else
            PlayerModel.Is_DG_Break = false
        end
        PlayerModel.DG_Stock = math.max(0, randomized_stocks)
        PlayerModel.DG_Point = PlayerModel.DG_Stock * 10000
    else
        -- point type
        local lower_bound = -60000
        local upper_bound = 60000
        if PlayerController.drive_randomizer.bounds_enabled then
            lower_bound = PlayerController.drive_randomizer.lower_bound_points
            upper_bound = PlayerController.drive_randomizer.upper_bound_points
        end
        local randomized_points = math.random(lower_bound, upper_bound)
        if randomized_points < 0 then
            randomized_points = -randomized_points
            PlayerModel.Is_DG_Break = true
        else
            PlayerModel.Is_DG_Break = false
        end
        PlayerModel.DG_Point = math.max(0, randomized_points)
        PlayerModel.DG_Stock = math.floor((PlayerModel.DG_Point + 5000) / 10000)
    end
end

function PlayerParam:randomize_player_super(PlayerIndex)
    if not self.controller[PlayerIndex].super_randomizer.enabled then
        return
    end

    local PlayerModel = self.model[PlayerIndex]
    local PlayerView = self.view[PlayerIndex]
    local PlayerController = self.controller[PlayerIndex]

    -- randomize super logic
    if PlayerView.super_type then
        -- stock type
        local lower_bound = 0
        local upper_bound = 3
        if PlayerController.super_randomizer.bounds_enabled then
            lower_bound = PlayerController.super_randomizer.lower_bound_stock
            upper_bound = PlayerController.super_randomizer.upper_bound_stock
        end
        local randomized_stocks = math.random(lower_bound, upper_bound)
        PlayerModel.SA_Stock = randomized_stocks
        PlayerModel.SA_Point = PlayerModel.SA_Stock * 10000
    else
        -- point type
        local lower_bound = 0
        local upper_bound = 30000
        if PlayerController.super_randomizer.bounds_enabled then
            lower_bound = PlayerController.super_randomizer.lower_bound_points
            upper_bound = PlayerController.super_randomizer.upper_bound_points
        end
        local randomized_points = math.random(lower_bound, upper_bound)
        PlayerModel.SA_Point = randomized_points
        PlayerModel.SA_Stock = math.floor((PlayerModel.SA_Point + 5000) / 10000)
    end
end

function PlayerParam:draw_health_ui(PlayerIndex)
    local PlayerView = self.view[PlayerIndex]
    local PlayerController = self.controller[PlayerIndex]
    local PlayerModel = self.model[PlayerIndex]
    local PlayerLabel = (PlayerIndex == "p1") and "Player 1" or "Player 2"

    -- draw the health UI

    -- health slider
    if PlayerController.health_randomizer.enabled then
        -- disable the slider if the randomizer is enabled
        imgui.begin_disabled()
    end
    PlayerView.health_changed, PlayerView.health =
        imgui.slider_int(PlayerLabel .. " Health Percentage", PlayerModel.Vital_Point, 0, 100)

    imgui.separator()

    if PlayerController.health_randomizer.enabled then
        -- stop the health slider disabled section
        imgui.end_disabled()
    end

    -- randomizer checkbox
    _, PlayerController.health_randomizer.enabled =
        imgui.checkbox("Toggle " .. PlayerLabel .. " Health Randomization", PlayerController.health_randomizer.enabled)

    if PlayerController.health_randomizer.enabled then
        _, PlayerController.health_randomizer.bounds_enabled =
            imgui.checkbox(
            "Enable Bounds for " .. PlayerLabel .. " Health Randomization",
            PlayerController.health_randomizer.bounds_enabled
        )
        if PlayerController.health_randomizer.bounds_enabled then
            -- show the bounds sliders
            _, PlayerController.health_randomizer.lower_bound =
                imgui.drag_int(
                PlayerLabel .. " Health Randomization Lower Bound",
                PlayerController.health_randomizer.lower_bound,
                0.3,
                0,
                PlayerController.health_randomizer.upper_bound
            )
            _, PlayerController.health_randomizer.upper_bound =
                imgui.drag_int(
                PlayerLabel .. " Health Randomization Upper Bound",
                PlayerController.health_randomizer.upper_bound,
                0.3,
                PlayerController.health_randomizer.lower_bound,
                100
            )
        end
    end
end

function PlayerParam:draw_drive_ui(PlayerIndex)
    local PlayerView = self.view[PlayerIndex]
    local PlayerController = self.controller[PlayerIndex]
    local PlayerModel = self.model[PlayerIndex]
    local PlayerLabel = (PlayerIndex == "p1") and "Player 1" or "Player 2"

    -- drive slider(s)

    if PlayerController.drive_randomizer.enabled then
        -- disable the slider if the randomizer is enabled
        imgui.begin_disabled()
    end

    -- drive sliders based on type
    if PlayerView.drive_type then
        -- stock type
        PlayerView.drive_stocks_changed, PlayerView.drive_stocks =
            imgui.slider_int(PlayerLabel .. " Drive Stocks", PlayerModel.DG_Stock, 0, 6)
    else
        -- custom type
        if PlayerController.drive_points_type then
            -- absolute type
            PlayerView.drive_points_changed, PlayerView.drive_points =
                imgui.drag_int(PlayerLabel .. " Drive Points", PlayerModel.DG_Point, 1, 0, 60000)
        else
            -- percentage type
            local points_increments = 0
            local current_points = PlayerModel.DG_Point / 10000
            PlayerView.drive_points_changed, points_increments =
                imgui.slider_float(
                PlayerLabel .. " Drive Points (stock increments of 10%)",
                current_points,
                0,
                6,
                "%.1f"
            )
            -- convert to points
            PlayerView.drive_points = math.floor(points_increments * 10000)
        end
    end

    -- burnout toggle
    PlayerView.burnout_changed, PlayerView.burnout =
        imgui.checkbox(PlayerLabel .. " Drive Burnout", PlayerModel.Is_DG_Break)

    imgui.separator()

    if PlayerController.drive_randomizer.enabled then
        -- stop the drive slider disabled section
        imgui.end_disabled()
    end

    -- stock vs points checkbox
    local type_value = PlayerModel.DG_Type == 1
    PlayerView.drive_type_changed, PlayerView.drive_type =
        imgui.checkbox("Toggle " .. PlayerLabel .. " Drive Type (Stock / Points)", type_value)

    -- on points, show percentage vs absolute toggle
    if not PlayerView.drive_type then
        imgui.same_line()
        _, PlayerController.drive_points_type =
            imgui.checkbox(
            "Toggle " .. PlayerLabel .. " Drive Points Type (Absolute / Percentage)",
            PlayerController.drive_points_type
        )
    end

    -- randomizer checkbox
    _, PlayerController.drive_randomizer.enabled =
        imgui.checkbox("Toggle " .. PlayerLabel .. " Drive Randomization", PlayerController.drive_randomizer.enabled)
    -- if randomizer bounds enable checkbox

    if PlayerController.drive_randomizer.enabled then
        -- show the bounds enable checkbox

        _, PlayerController.drive_randomizer.bounds_enabled =
            imgui.checkbox(
            "Enable Bounds for " .. PlayerLabel .. " Drive Randomization",
            PlayerController.drive_randomizer.bounds_enabled
        )

        if PlayerController.drive_randomizer.bounds_enabled then
            -- show the bounds sliders based on type

            if PlayerView.drive_type then
                -- stock type
                _, PlayerController.drive_randomizer.lower_bound_stock =
                    imgui.drag_int(
                    PlayerLabel .. " Drive Stock Randomization Lower Bound",
                    PlayerController.drive_randomizer.lower_bound_stock,
                    0.3,
                    -6,
                    PlayerController.drive_randomizer.upper_bound_stock
                )
                _, PlayerController.drive_randomizer.upper_bound_stock =
                    imgui.drag_int(
                    PlayerLabel .. " Drive Stock Randomization Upper Bound",
                    PlayerController.drive_randomizer.upper_bound_stock,
                    0.3,
                    PlayerController.drive_randomizer.lower_bound_stock,
                    6
                )
            else
                -- point type
                if PlayerController.drive_points_type then
                    -- absolute type
                    _, PlayerController.drive_randomizer.lower_bound_points =
                        imgui.drag_int(
                        PlayerLabel .. " Drive Points Randomization Lower Bound",
                        PlayerController.drive_randomizer.lower_bound_points,
                        1,
                        -60000,
                        PlayerController.drive_randomizer.upper_bound_points
                    )
                    _, PlayerController.drive_randomizer.upper_bound_points =
                        imgui.drag_int(
                        PlayerLabel .. " Drive Points Randomization Upper Bound",
                        PlayerController.drive_randomizer.upper_bound_points,
                        1,
                        PlayerController.drive_randomizer.lower_bound_points,
                        60000
                    )
                else
                    -- percentage type
                    local points_increments_lb = 0
                    local current_points_lb = PlayerController.drive_randomizer.lower_bound_points / 10000

                    local points_increments_ub = 0
                    local current_points_ub = PlayerController.drive_randomizer.upper_bound_points / 10000

                    _, points_increments_lb =
                        imgui.drag_float(
                        PlayerLabel .. " Drive Points Randomization Lower Bound (stock increments of 10%)",
                        current_points_lb,
                        0.1,
                        -6,
                        current_points_ub,
                        "%.1f"
                    )
                    PlayerController.drive_randomizer.lower_bound_points = math.floor(points_increments_lb * 10000)

                    _, points_increments_ub =
                        imgui.drag_float(
                        PlayerLabel .. " Drive Points Randomization Upper Bound (stock increments of 10%)",
                        current_points_ub,
                        0.1,
                        points_increments_lb,
                        6,
                        "%.1f"
                    )
                    PlayerController.drive_randomizer.upper_bound_points = math.floor(points_increments_ub * 10000)
                end
            end
        end
    end
end

function PlayerParam:draw_super_ui(PlayerIndex)
    local PlayerView = self.view[PlayerIndex]
    local PlayerController = self.controller[PlayerIndex]
    local PlayerModel = self.model[PlayerIndex]
    local PlayerLabel = (PlayerIndex == "p1") and "Player 1" or "Player 2"

    -- super slider(s)
    if PlayerController.super_randomizer.enabled then
        -- disable the slider if the randomizer is enabled
        imgui.begin_disabled()
    end

    -- super sliders based on type
    if PlayerView.super_type then
        -- stock type
        PlayerView.super_stocks_changed, PlayerView.super_stocks =
            imgui.slider_int(PlayerLabel .. " Super Stocks", PlayerModel.SA_Stock, 0, 3)
    else
        -- custom type
        if PlayerController.super_points_type then
            -- absolute type
            PlayerView.super_points_changed, PlayerView.super_points =
                imgui.drag_int(PlayerLabel .. " Super Points", PlayerModel.SA_Point, 1, 0, 30000)
        else
            -- percentage type
            local points_increments = 0
            local current_points = PlayerModel.SA_Point / 10000
            PlayerView.super_points_changed, points_increments =
                imgui.slider_float(
                PlayerLabel .. " Super Points (stock increments of 10%)",
                current_points,
                0,
                3,
                "%.1f"
            )
            -- convert to points
            PlayerView.super_points = math.floor(points_increments * 10000)
        end
    end

    imgui.separator()

    if PlayerController.super_randomizer.enabled then
        imgui.end_disabled()
    end

    -- stock vs points checkbox
    local type_value = PlayerModel.SA_Type == 1
    PlayerView.super_type_changed, PlayerView.super_type =
        imgui.checkbox("Toggle " .. PlayerLabel .. " Super Type (Stock / Points)", type_value)

    -- on points, show percentage vs absolute toggle
    if not PlayerView.super_type then
        imgui.same_line()
        _, PlayerController.super_points_type =
            imgui.checkbox(
            "Toggle " .. PlayerLabel .. " Super Points Type (Absolute / Percentage)",
            PlayerController.super_points_type
        )
    end

    -- randomizer checkbox
    _, PlayerController.super_randomizer.enabled =
        imgui.checkbox("Toggle " .. PlayerLabel .. " Super Randomization", PlayerController.super_randomizer.enabled)
    -- if randomizer bounds enable checkbox
    if PlayerController.super_randomizer.enabled then
        -- show the bounds enable checkbox

        _, PlayerController.super_randomizer.bounds_enabled =
            imgui.checkbox(
            "Enable Bounds for " .. PlayerLabel .. " Super Randomization",
            PlayerController.super_randomizer.bounds_enabled
        )

        if PlayerController.super_randomizer.bounds_enabled then
            -- show the bounds sliders based on type

            if PlayerView.super_type then
                -- stock type
                _, PlayerController.super_randomizer.lower_bound_stock =
                    imgui.drag_int(
                    PlayerLabel .. " Super Stock Randomization Lower Bound",
                    PlayerController.super_randomizer.lower_bound_stock,
                    0.3,
                    0,
                    PlayerController.super_randomizer.upper_bound_stock
                )
                _, PlayerController.super_randomizer.upper_bound_stock =
                    imgui.drag_int(
                    PlayerLabel .. " Super Stock Randomization Upper Bound",
                    PlayerController.super_randomizer.upper_bound_stock,
                    0.3,
                    PlayerController.super_randomizer.lower_bound_stock,
                    3
                )
            else
                -- point type
                if PlayerController.super_points_type then
                    -- absolute type
                    _, PlayerController.super_randomizer.lower_bound_points =
                        imgui.drag_int(
                        PlayerLabel .. " Super Points Randomization Lower Bound",
                        PlayerController.super_randomizer.lower_bound_points,
                        1,
                        0,
                        PlayerController.super_randomizer.upper_bound_points
                    )
                    _, PlayerController.super_randomizer.upper_bound_points =
                        imgui.drag_int(
                        PlayerLabel .. " Super Points Randomization Upper Bound",
                        PlayerController.super_randomizer.upper_bound_points,
                        1,
                        PlayerController.super_randomizer.lower_bound_points,
                        30000
                    )
                else
                    -- percentage type
                    local points_increments_lb = 0
                    local current_points_lb = PlayerController.super_randomizer.lower_bound_points / 10000

                    local points_increments_ub = 0
                    local current_points_ub = PlayerController.super_randomizer.upper_bound_points / 10000

                    _, points_increments_lb =
                        imgui.drag_float(
                        PlayerLabel .. " Super Points Randomization Lower Bound (stock increments of 10%)",
                        current_points_lb,
                        0.1,
                        0,
                        current_points_ub,
                        "%.1f"
                    )
                    PlayerController.super_randomizer.lower_bound_points = math.floor(points_increments_lb * 10000)
                    _, points_increments_ub =
                        imgui.drag_float(
                        PlayerLabel .. " Super Points Randomization Upper Bound (stock increments of 10%)",
                        current_points_ub,
                        0.1,
                        points_increments_lb,
                        3,
                        "%.1f"
                    )
                    PlayerController.super_randomizer.upper_bound_points = math.floor(points_increments_ub * 10000)
                end
            end
        end
    end
end

function PlayerParam:init(ParameterSettingsData)
    self:init_player("p1", ParameterSettingsData.PlayerDatas[0])
    self:init_player("p2", ParameterSettingsData.PlayerDatas[1])
end

function PlayerParam:draw_ui()
    if imgui.tree_node("Health") then
        if imgui.tree_node("Player 1 Health") then
            self:draw_health_ui("p1")
            imgui.tree_pop()
        end
        if imgui.tree_node("Player 2 Health") then
            self:draw_health_ui("p2")
            imgui.tree_pop()
        end
        imgui.tree_pop()
    end
    if imgui.tree_node("Drive") then
        if imgui.tree_node("Player 1 Drive") then
            self:draw_drive_ui("p1")
            imgui.tree_pop()
        end
        if imgui.tree_node("Player 2 Drive") then
            self:draw_drive_ui("p2")
            imgui.tree_pop()
        end
        imgui.tree_pop()
    end
    if imgui.tree_node("Super") then
        if imgui.tree_node("Player 1 Super") then
            self:draw_super_ui("p1")
            imgui.tree_pop()
        end
        if imgui.tree_node("Player 2 Super") then
            self:draw_super_ui("p2")
            imgui.tree_pop()
        end
        imgui.tree_pop()
    end
end

function PlayerParam:update()
    -- update logic for player parameters

    local need_apply = false

    need_apply = self:update_player_parameters("p1") or need_apply
    need_apply = self:update_player_parameters("p2") or need_apply

    return need_apply
end

function PlayerParam:randomize()
    -- randomization logic for player parameters
    self:randomize_player_health("p1")
    self:randomize_player_health("p2")
    self:randomize_player_drive("p1")
    self:randomize_player_drive("p2")
    self:randomize_player_super("p1")
    self:randomize_player_super("p2")
end

--[[
    Unique character gauges
]]
module.data.UniqueCharData = require("TrainingModePlusScripts/UniqueCharacterParametersData")

local UniqueGaugeParam = {
    model = {},
    view = {},
    controller = {}
}

function UniqueGaugeParam:init(ParameterSettingsData)
    -- unique gauge parameter initialization
    self.model = ParameterSettingsData

    for _, charData in pairs(module.data.UniqueCharData) do
        self.view[charData.name] = {}
        self.controller[charData.name] = {}
        -- initialize timers ui data
        if charData.timers then
            for _, timerData in pairs(charData.timers) do
                self.view[charData.name][timerData.id] = {}
                self.controller[charData.name][timerData.id] = {}

                self.view[charData.name][timerData.id].timer_combo_value = 1
                self.view[charData.name][timerData.id].timer_combo_changed = false

                -- randomizer settings
                self.controller[charData.name][timerData.id].randomizer_enabled = false

                if timerData.install == true then
                    -- this is the only case we need to store the start value
                    self.controller[charData.name][timerData.id].installed_start_value = timerData.timerMaxValue
                    self.view[charData.name][timerData.id].installed_start_value_changed = false

                    -- randomizer for install timers

                    -- probability of it being disabled when randomized, default is 50%
                    self.controller[charData.name][timerData.id].disabled_prob_percentage = 50

                    self.controller[charData.name][timerData.id].bounds_enabled = false
                    self.controller[charData.name][timerData.id].lower_bound = 0
                    self.controller[charData.name][timerData.id].upper_bound = timerData.timerMaxValue
                end
            end
        end
        -- initialize stocks ui data
        if charData.stocks then
            for _, stockData in pairs(charData.stocks) do
                self.view[charData.name][stockData.id] = {}
                self.controller[charData.name][stockData.id] = {}

                self.view[charData.name][stockData.id].stock_slider = 0
                self.view[charData.name][stockData.id].stock_slider_changed = false
                self.view[charData.name][stockData.id].infinite_checkbox = false
                self.view[charData.name][stockData.id].infinite_checkbox_changed = false

                -- randomizer settings
                self.controller[charData.name][stockData.id].randomizer_enabled = false
                self.controller[charData.name][stockData.id].bounds_enabled = false
                self.controller[charData.name][stockData.id].lower_bound = stockData.minValue
                self.controller[charData.name][stockData.id].upper_bound = stockData.maxValue
            end
        end
    end
end

function UniqueGaugeParam:update()
    local char_id1 = module.data.SelectMenu.PlayerDatas[0].FighterID
    local char_id2 = module.data.SelectMenu.PlayerDatas[1].FighterID
    local char_datas
    if char_id1 == char_id2 then
        char_datas = {module.data.UniqueCharData[char_id1]}
    else
        char_datas = {module.data.UniqueCharData[char_id1], module.data.UniqueCharData[char_id2]}
    end

    local need_refresh = false
    -- unique gauge parameter update logic
    for index, char_data in pairs(char_datas) do
        if char_data then
            -- update timers
            if char_data.timers then
                for _, timerData in pairs(char_data.timers) do
                    local ui_timer = self.view[char_data.name][timerData.id]
                    if ui_timer.timer_combo_changed then
                        -- set the unique gauge data based on the selected value
                        self.model[timerData.id] = ui_timer.timer_combo_value - 1
                        need_refresh = true
                    end
                    -- add logic for the timer later
                    if timerData.install == true and ui_timer.timer_combo_value == 2 then
                        -- set the installed start value somewhere
                        local liveData = nil
                        if index == 1 then
                            liveData = module.data.live_P1
                        else
                            liveData = module.data.live_P2
                        end
                        if module.data.sGame.stage_timer == 1 then
                            liveData.style_timer = self.controller[char_data.name][timerData.id].installed_start_value
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
                    local ui_stock = self.view[char_data.name][stockData.id]
                    if ui_stock.infinite_checkbox_changed then
                        if ui_stock.infinite_checkbox then
                            self.model[stockData.id] = 7
                        else
                            self.model[stockData.id] = ui_stock.stock_slider
                        end
                        need_refresh = true
                    end
                    if ui_stock.stock_slider_changed then
                        self.model[stockData.id] = ui_stock.stock_slider
                        need_refresh = true
                    end
                end
            end
        end
    end
    return need_refresh
end

function UniqueGaugeParam:randomize()
    local char_id1 = module.data.SelectMenu.PlayerDatas[0].FighterID
    local char_id2 = module.data.SelectMenu.PlayerDatas[1].FighterID
    local char_datas
    if char_id1 == char_id2 then
        char_datas = {module.data.UniqueCharData[char_id1]}
    else
        char_datas = {module.data.UniqueCharData[char_id1], module.data.UniqueCharData[char_id2]}
    end

    -- unique gauge parameter randomization logic
    for _, char_data in pairs(char_datas) do
        if char_data then
            -- randomize timers
            if char_data.timers then
                for _, timerData in pairs(char_data.timers) do
                    local controller = self.controller[char_data.name][timerData.id]
                    if controller.randomizer_enabled then
                        -- first calculate the disabled probability
                        local rand_percentage = math.random(0, 100)
                        if timerData.install == true and rand_percentage < controller.disabled_prob_percentage then
                            -- set to disabled
                            self.model[timerData.id] = 0
                        else
                            -- enable it
                            self.model[timerData.id] = 1

                            -- if it's an install timer, set the start value
                            if timerData.install == true then
                                -- randomize the timer starting value
                                local lower_bound = 0
                                local upper_bound = timerData.timerMaxValue
                                if controller.bounds_enabled then
                                    lower_bound = controller.lower_bound
                                    upper_bound = controller.upper_bound
                                end

                                self.controller[char_data.name][timerData.id].installed_start_value =
                                    math.random(lower_bound, upper_bound)
                            end
                        end
                    end
                end
            end

            -- randomize stocks
            if char_data.stocks then
                for _, stockData in pairs(char_data.stocks) do
                    local controller = self.controller[char_data.name][stockData.id]
                    if controller.randomizer_enabled then
                        local lower_bound = stockData.minValue
                        local upper_bound = stockData.maxValue
                        if controller.bounds_enabled then
                            lower_bound = controller.lower_bound
                            upper_bound = controller.upper_bound
                        end
                        local random_value = math.random(lower_bound, upper_bound)
                        self.model[stockData.id] = random_value
                    end
                end
            end
        end
    end
end

function UniqueGaugeParam:draw_ui()
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
            imgui.text("Character: " .. char_data.name)
            -- draw timers
            if char_data.timers then
                for _, timerData in pairs(char_data.timers) do
                    if any_installed_timer then
                        imgui.separator()
                    end
                    any_installed_timer = true

                    local current_value = self.model[timerData.id]
                    -- use stored timer ui values
                    local ui_timer = self.view[char_data.name][timerData.id]
                    local descriptor = timerData.descriptors[current_value + 1]
                    imgui.text(timerData.name .. ": " .. descriptor)

                    local controller = self.controller[char_data.name][timerData.id]

                    if controller.randomizer_enabled then
                        imgui.begin_disabled()
                    end

                    -- use stored slider value as current so it persists
                    ui_timer.timer_combo_changed, ui_timer.timer_combo_value =
                        imgui.combo(timerData.name .. " Value ", current_value + 1, timerData.descriptors)
                    if timerData.install == true and current_value == 1 then
                        -- installed timer UI elements
                        ui_timer.installed_start_value_changed,
                            self.controller[char_data.name][timerData.id].installed_start_value =
                            imgui.slider_int(
                            timerData.name .. " starting activation value",
                            self.controller[char_data.name][timerData.id].installed_start_value,
                            0,
                            timerData.timerMaxValue
                        )
                        if imgui.is_item_active() then
                            module.ui_active = true
                        end
                    end

                    if controller.randomizer_enabled then
                        imgui.end_disabled()
                    end

                    if current_value ~= 2 then
                        -- randomizer checkbox
                        _, controller.randomizer_enabled =
                            imgui.checkbox(
                            "Toggle " .. timerData.name .. " Randomization",
                            controller.randomizer_enabled
                        )

                        if controller.randomizer_enabled and timerData.install == true then
                            -- randomizer disabled probability
                            _, controller.disabled_prob_percentage =
                                imgui.slider_int(
                                "Probability (%) of " .. timerData.name .. " being disabled when randomized",
                                controller.disabled_prob_percentage,
                                0,
                                100
                            )

                            -- bounds only enabled for installs with timer, no reason to have them otherwise
                            if timerData.install == true then
                                -- bounds enable checkbox
                                _, controller.bounds_enabled =
                                    imgui.checkbox(
                                    "Enable Bounds for " .. timerData.name .. " Randomization",
                                    controller.bounds_enabled
                                )
                                if controller.bounds_enabled then
                                    -- show the bounds sliders
                                    _, controller.lower_bound =
                                        imgui.drag_int(
                                        timerData.name .. " Randomization Lower Bound",
                                        controller.lower_bound,
                                        0.3,
                                        0,
                                        controller.upper_bound
                                    )
                                    _, controller.upper_bound =
                                        imgui.drag_int(
                                        timerData.name .. " Randomization Upper Bound",
                                        controller.upper_bound,
                                        0.3,
                                        controller.lower_bound,
                                        timerData.timerMaxValue
                                    )
                                end
                            end
                        end
                    end
                end
            end

            -- draw stocks
            if char_data.stocks then
                for _, stockData in pairs(char_data.stocks) do
                    if any_installed_timer then
                        imgui.separator()
                    end
                    any_installed_timer = true

                    local current_value = self.model[stockData.id]
                    -- check for value == 7 (infinite)
                    local descriptor
                    if current_value == 7 then
                        descriptor = "Infinite"
                    else
                        descriptor = stockData.descriptors[current_value + 1]
                    end
                    -- use stored stock ui values
                    local ui_stock = self.view[char_data.name][stockData.id]
                    imgui.text(stockData.name .. ": " .. descriptor)

                    local controller = self.controller[char_data.name][stockData.id]

                    if controller.randomizer_enabled then
                        imgui.begin_disabled()
                    end

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

                        imgui.separator()
                    end

                    if controller.randomizer_enabled then
                        imgui.end_disabled()
                    end

                    if current_value ~= 7 then
                        -- randomizer checkbox
                        _, controller.randomizer_enabled =
                            imgui.checkbox(
                            "Toggle " .. stockData.name .. " Randomization",
                            controller.randomizer_enabled
                        )

                        if controller.randomizer_enabled then
                            _, controller.bounds_enabled =
                                imgui.checkbox(
                                "Enable Bounds for " .. stockData.name .. " Randomization",
                                controller.bounds_enabled
                            )
                            if controller.bounds_enabled then
                                -- show the bounds sliders
                                _, controller.lower_bound =
                                    imgui.drag_int(
                                    stockData.name .. " Randomization Lower Bound",
                                    controller.lower_bound,
                                    0.3,
                                    stockData.minValue,
                                    controller.upper_bound
                                )
                                _, controller.upper_bound =
                                    imgui.drag_int(
                                    stockData.name .. " Randomization Upper Bound",
                                    controller.upper_bound,
                                    0.3,
                                    controller.lower_bound,
                                    stockData.maxValue
                                )
                            end
                        end
                    end
                end
            end
        end
    end
    if not any_installed_timer then
        imgui.text_colored("Not available for these characters", 0xFF00A9F9)
    end
end

--[[
    Position parameters 
]]
module.data.PositionParametersData = require("TrainingModePlusScripts/PositionParametersData")

local PositionalParam = {
    model = {},
    view = {},
    controller = {}
}

function PositionalParam:init(SelectMenuData)
    -- positional parameter initialization
    self.model = SelectMenuData

    -- determine current character ids from the passed SelectMenuData
    local char_id1 = SelectMenuData.PlayerDatas[0].FighterID
    local char_id2 = SelectMenuData.PlayerDatas[1].FighterID

    local offset_data_1 = module.data.PositionParametersData.character_relative_distance_offsets[char_id1]
    local offset_data_2 = module.data.PositionParametersData.character_relative_distance_offsets[char_id2]

    local minimum_offset1 = offset_data_1 and offset_data_1.minimum_distance_offset or 0.0
    local minimum_offset2 = offset_data_2 and offset_data_2.minimum_distance_offset or 0.0
    local position_relative_offset_1 = offset_data_1 and offset_data_1.position_vs_relative_distance_offset or 0.0
    local position_relative_offset_2 = offset_data_2 and offset_data_2.position_vs_relative_distance_offset or 0.0
    --[[
        controller variables
    ]]
    -- controller encompasses most settings as they don't translate well to the ingame parameters
    self.controller.relative_distance = {}
    self.controller.screen_position = {}
    self.controller.randomizer = {}

    -- relative distance
    self.controller.relative_distance.min =
        module.data.PositionParametersData.default_relative_distance.min + minimum_offset1 + minimum_offset2
    -- account for character-specific offsets: subtract total_offset from the allowed max
    self.controller.relative_distance.max =
        module.data.PositionParametersData.default_relative_distance.max -
        (position_relative_offset_1 + position_relative_offset_2)

    if SelectMenuData.StartLocation == 0 then
        -- midscreen start
        self.controller.relative_distance.relative_distance = 300
        self.controller.relative_distance.discrete_relative_distance_preset_index = 6
    else
        -- corner start
        self.controller.relative_distance.relative_distance = self.controller.relative_distance.min
        self.controller.relative_distance.discrete_relative_distance_preset_index = 1
    end

    self.controller.relative_distance.enabled = false

    self.controller.relative_distance.discrete_enabled = true

    -- absolute screen position

    self.controller.screen_position.enabled = false

    self.controller.screen_position.position = 0.0
    self.controller.screen_position.pivot_type_index = 1
    -- discrete vs precise
    self.controller.screen_position.discrete_screen_position = true
    self.controller.screen_position.discrete_screen_position_value = 0
    -- absolute, distance from left/distance from right
    self.controller.screen_position.precise_distance_reference_index = 1

    --[[
        view variables
    ]]
    self.view.relative_distance = {}
    self.view.screen_position = {}
    self.view.randomizer = {}

    -- relative distance

    self.view.relative_distance.relative_distance_enabled_changed = false

    self.view.relative_distance.discrete_relative_distance_enabled_changed = false

    self.view.relative_distance.relative_distance_changed = false
    self.view.relative_distance.discrete_relative_distance_preset_index_changed = false

    self.view.relative_distance.old_starting_position = SelectMenuData.StartLocation

    -- screen position
    self.view.screen_position.enabled_changed = false

    self.view.screen_position.position_changed = false
    self.view.screen_position.pivot_type_index_changed = false
    self.view.screen_position.discrete_screen_position_changed = false
    self.view.screen_position.discrete_screen_position_value_changed = false
    self.view.screen_position.precise_distance_reference_index_changed = false

    self.view.screen_position.show_position_warning = false
    self.view.screen_position.precise_distance_reference_previous_index =
        self.controller.screen_position.precise_distance_reference_index

    --[[
        randomizer variables
    ]]
    self.controller.randomizer.enabled_relative = false
    self.controller.randomizer.relative_bounds_enabled = false
    self.controller.randomizer.relative_lower_bound = self.controller.relative_distance.min
    self.controller.randomizer.relative_upper_bound = self.controller.relative_distance.max
    self.controller.randomizer.relative_lower_bound_discrete_index = 1
    self.controller.randomizer.relative_upper_bound_discrete_index =
        #module.data.PositionParametersData.preset_relative_distance_offsets.names

    self.view.randomizer.relative_discrete_bounds_changed = false

    self.controller.randomizer.enabled_screen = false
    self.controller.randomizer.screen_bounds_enabled = false
    self.controller.randomizer.screen_lower_bound = module.data.PositionParametersData.default_screen_position.min
    self.controller.randomizer.screen_upper_bound = module.data.PositionParametersData.default_screen_position.max
    self.controller.randomizer.screen_lower_bound_discrete = -6
    self.controller.randomizer.screen_upper_bound_discrete = 6

    self.view.randomizer.screen_discrete_bounds_changed = false
    self.view.randomizer.screen_continuous_bounds_changed = false

    -- P1 vs P2 side
    self.controller.randomizer.sideswitch_enabled = false

    --[[
        HOOKS
    ]]
    local function on_pre(args)
        -- no pre logic needed
    end
    local function on_post(retval)
        if not self.controller.relative_distance.enabled then
            -- if its disabled
            return
        end

        -- get the current characters to determine the offset to the relative distances
        local char_id1 = self.model.PlayerDatas[0].FighterID
        local char_id2 = self.model.PlayerDatas[1].FighterID

        local offset_data_1 = module.data.PositionParametersData.character_relative_distance_offsets[char_id1]
        local offset_data_2 = module.data.PositionParametersData.character_relative_distance_offsets[char_id2]
        local position_relative_offset_1 = offset_data_1 and offset_data_1.position_vs_relative_distance_offset or 0.0
        local position_relative_offset_2 = offset_data_2 and offset_data_2.position_vs_relative_distance_offset or 0.0
        local total_offset = position_relative_offset_1 + position_relative_offset_2

        -- if starting position adjustment is enabled, force custom start location
        if self.controller.screen_position.enabled then
            self.model.StartLocation = 3
        else
            if self.model.StartLocation ~= 3 then
                -- if starting position adjustment is disabled, we just use the old starting position
                self.view.relative_distance.old_starting_position = self.model.StartLocation
            else
                -- if starting position adjustment is disabled, we just use the old starting position
                self.model.StartLocation = self.view.relative_distance.old_starting_position
            end
        end

        if self.model.StartLocation == 0 then
            -- center pivot
            local rel_distance = self.controller.relative_distance.relative_distance
            self.model.PlayerDatas[0].ManualPosX = -math.floor(rel_distance / 2.0) - (total_offset / 2.0)
            self.model.PlayerDatas[1].ManualPosX = math.ceil(rel_distance / 2.0) + (total_offset / 2.0)
        elseif self.model.StartLocation == 1 then
            -- right side pivot
            local right_position = module.data.PositionParametersData.default_screen_position.max
            self.model.PlayerDatas[0].ManualPosX =
                right_position - self.controller.relative_distance.relative_distance - total_offset
            self.model.PlayerDatas[1].ManualPosX = right_position
        elseif self.model.StartLocation == 2 then
            -- left side pivot
            local left_position = module.data.PositionParametersData.default_screen_position.min
            self.model.PlayerDatas[0].ManualPosX = left_position
            self.model.PlayerDatas[1].ManualPosX =
                left_position + self.controller.relative_distance.relative_distance + total_offset
        elseif self.model.StartLocation == 3 then
            -- custom position pivot
            local new_pos1
            local new_pos2

            -- first, calculate the position of the fulcrum based on the distance reference
            local fulcrum_position = 0.0
            if self.controller.screen_position.precise_distance_reference_index == 1 then
                -- absolute position
                fulcrum_position = self.controller.screen_position.position
            elseif self.controller.screen_position.precise_distance_reference_index == 2 then
                -- distance from left corner
                fulcrum_position =
                    self.controller.screen_position.position +
                    module.data.PositionParametersData.default_screen_position.min
            elseif self.controller.screen_position.precise_distance_reference_index == 3 then
                -- distance from right corner
                fulcrum_position =
                    module.data.PositionParametersData.default_screen_position.max -
                    self.controller.screen_position.position
            end

            -- calculate the new positions based on the pivot type
            if self.controller.screen_position.pivot_type_index == 1 then
                -- center pivot type
                new_pos1 = fulcrum_position - (self.controller.relative_distance.relative_distance + total_offset) / 2.0
                new_pos2 = fulcrum_position + (self.controller.relative_distance.relative_distance + total_offset) / 2.0
            elseif self.controller.screen_position.pivot_type_index == 2 then
                -- p1 player pivot type
                new_pos1 = fulcrum_position
                new_pos2 = fulcrum_position + self.controller.relative_distance.relative_distance + total_offset
            elseif self.controller.screen_position.pivot_type_index == 3 then
                -- p2 player pivot type
                new_pos1 = fulcrum_position - self.controller.relative_distance.relative_distance - total_offset
                new_pos2 = fulcrum_position
            end

            -- check screen bounds
            local screen_min = module.data.PositionParametersData.default_screen_position.min
            local screen_max = module.data.PositionParametersData.default_screen_position.max
            if new_pos1 < screen_min then
                new_pos1 = screen_min
                new_pos2 = screen_min + self.controller.relative_distance.relative_distance + total_offset
            elseif new_pos2 > screen_max then
                new_pos2 = screen_max
                new_pos1 = screen_max - self.controller.relative_distance.relative_distance - total_offset
            end
            self.model.PlayerDatas[0].ManualPosX = new_pos1
            self.model.PlayerDatas[1].ManualPosX = new_pos2
        end

        self.model.StartLocation = 3
    end

    self.update_positioning_func = on_post

    sdk.hook(
        sdk.find_type_definition("app.training.tf_SelectMenu.FuncData"):get_method("ChangeStartLocationType"),
        on_pre,
        on_post
    )
end

function PositionalParam:update()
    -- positional parameter update logic
    local need_refresh = false

    -- get the current characters to determine the offset to the relative distances
    local char_id1 = self.model.PlayerDatas[0].FighterID
    local char_id2 = self.model.PlayerDatas[1].FighterID

    local offset_data_1 = module.data.PositionParametersData.character_relative_distance_offsets[char_id1]
    local offset_data_2 = module.data.PositionParametersData.character_relative_distance_offsets[char_id2]

    local minimum_offset1 = offset_data_1 and offset_data_1.minimum_distance_offset or 0.0
    local minimum_offset2 = offset_data_2 and offset_data_2.minimum_distance_offset or 0.0
    local position_relative_offset_1 = offset_data_1 and offset_data_1.position_vs_relative_distance_offset or 0.0
    local position_relative_offset_2 = offset_data_2 and offset_data_2.position_vs_relative_distance_offset or 0.0

    -- relative distance updates
    self.controller.relative_distance.min =
        module.data.PositionParametersData.default_relative_distance.min + minimum_offset1 + minimum_offset2
    -- keep max within screen-space minus the total character offset
    self.controller.relative_distance.max =
        module.data.PositionParametersData.default_relative_distance.max -
        (position_relative_offset_1 + position_relative_offset_2)

    self.controller.relative_distance.relative_distance =
        math.max(
        self.controller.relative_distance.min,
        math.min(self.controller.relative_distance.relative_distance, self.controller.relative_distance.max)
    )

    -- relative distance changed
    if self.view.relative_distance.relative_distance_enabled_changed then
        need_refresh = true
        if self.controller.relative_distance.enabled then
            -- enable relative distance adjustments
            self.view.relative_distance.old_starting_position = self.model.StartLocation
        else
            -- disable relative distance adjustments
            -- revert to old starting position
            self.model.StartLocation = self.view.relative_distance.old_starting_position
        end
    end

    -- relative distance discrete preset values
    if self.view.relative_distance.discrete_relative_distance_preset_index_changed then
        need_refresh = true

        -- if its the last value, we set the maximum (which we compute dynamically), otherwise we use the preset values in the table
        if
            self.controller.relative_distance.discrete_relative_distance_preset_index ==
                #module.data.PositionParametersData.preset_relative_distance_offsets.values + 1
         then
            self.controller.relative_distance.relative_distance = self.controller.relative_distance.max
        else
            -- preset values are offsets from the minimum, add the min to get the absolute relative distance
            self.controller.relative_distance.relative_distance =
                self.controller.relative_distance.min +
                module.data.PositionParametersData.preset_relative_distance_offsets.values[
                    self.controller.relative_distance.discrete_relative_distance_preset_index
                ]
        end
    end

    if self.view.screen_position.enabled_changed then
        need_refresh = true

        if self.view.screen_position.enabled then
            -- if we disabled starting position adjustment, restore the old starting position
            self.view.relative_distance.old_starting_position = 3
        else
            -- when we disable starting position, we just default back to the middle of the screen, cuz it don't matter
            self.view.relative_distance.old_starting_position = 0
        end
    end

    -- for the screen position, when we change the reference type, we need to adjust the position value to match the new reference
    if self.view.screen_position.precise_distance_reference_index_changed then
        local previous_index = self.view.screen_position.precise_distance_reference_previous_index
        local current_value = self.controller.screen_position.position

        local fulcrum_position = 0.0
        -- first, get the fulcrum position based on the previous reference
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

        -- now, calculate the new starting position based on the new reference
        if self.controller.screen_position.precise_distance_reference_index == 1 then
            -- absolute position
            self.controller.screen_position.position = fulcrum_position
        elseif self.controller.screen_position.precise_distance_reference_index == 2 then
            -- distance from left corner
            self.controller.screen_position.position =
                fulcrum_position - module.data.PositionParametersData.default_screen_position.min
        elseif self.controller.screen_position.precise_distance_reference_index == 3 then
            -- distance from right corner
            self.controller.screen_position.position =
                module.data.PositionParametersData.default_screen_position.max - fulcrum_position
        end

        self.view.screen_position.precise_distance_reference_previous_index =
            self.controller.screen_position.precise_distance_reference_index

        -- convert the randomizer bounds as well
        if self.controller.randomizer.screen_bounds_enabled then
            local lower_fulcrum = 0.0
            local upper_fulcrum = 0.0

            -- get fulcrum positions based on previous reference
            if previous_index == 1 then
                -- absolute position
                lower_fulcrum = self.controller.randomizer.screen_lower_bound
                upper_fulcrum = self.controller.randomizer.screen_upper_bound
            elseif previous_index == 2 then
                -- distance from left corner
                lower_fulcrum =
                    self.controller.randomizer.screen_lower_bound +
                    module.data.PositionParametersData.default_screen_position.min
                upper_fulcrum =
                    self.controller.randomizer.screen_upper_bound +
                    module.data.PositionParametersData.default_screen_position.min
            elseif previous_index == 3 then
                -- distance from right corner
                upper_fulcrum =
                    module.data.PositionParametersData.default_screen_position.max -
                    self.controller.randomizer.screen_lower_bound
                lower_fulcrum =
                    module.data.PositionParametersData.default_screen_position.max -
                    self.controller.randomizer.screen_upper_bound
            end

            -- now convert to new reference
            if self.controller.screen_position.precise_distance_reference_index == 1 then
                -- absolute position
                self.controller.randomizer.screen_lower_bound = lower_fulcrum
                self.controller.randomizer.screen_upper_bound = upper_fulcrum
            elseif self.controller.screen_position.precise_distance_reference_index == 2 then
                -- distance from left corner
                self.controller.randomizer.screen_lower_bound =
                    lower_fulcrum - module.data.PositionParametersData.default_screen_position.min
                self.controller.randomizer.screen_upper_bound =
                    upper_fulcrum - module.data.PositionParametersData.default_screen_position.min
            elseif self.controller.screen_position.precise_distance_reference_index == 3 then
                -- distance from right corner
                self.controller.randomizer.screen_upper_bound =
                    module.data.PositionParametersData.default_screen_position.max - lower_fulcrum
                self.controller.randomizer.screen_lower_bound =
                    module.data.PositionParametersData.default_screen_position.max - upper_fulcrum
            end
        end
    end

    -- if the discrete starting-position reference slider changed, convert it to an absolute position value
    if self.view.screen_position.discrete_screen_position_value_changed then
        need_refresh = true
        local min_pos = module.data.PositionParametersData.default_screen_position.min
        local max_pos = module.data.PositionParametersData.default_screen_position.max
        local discrete_value = self.controller.screen_position.discrete_screen_position_value
        -- map -6..6 to min_pos..max_pos (13 steps -> denominator 12)
        local fulcrum_position = min_pos + ((discrete_value + 6) / 12) * (max_pos - min_pos)

        -- store controller.screen_position.position according to the currently selected reference type
        -- 1 = absolute position, 2 = distance from left corner, 3 = distance from right corner
        if self.controller.screen_position.precise_distance_reference_index == 1 then
            -- absolute
            self.controller.screen_position.position = fulcrum_position
        elseif self.controller.screen_position.precise_distance_reference_index == 2 then
            -- distance from left corner
            self.controller.screen_position.position = fulcrum_position - min_pos
        else
            -- distance from right corner
            self.controller.screen_position.position = max_pos - fulcrum_position
        end
    end

    if self.view.screen_position.position_changed then
        need_refresh = true
        -- update the discrete slider value so the UI matches the computed fulcrum position
        local min_pos = module.data.PositionParametersData.default_screen_position.min
        local max_pos = module.data.PositionParametersData.default_screen_position.max

        local fulcrum_position = 0.0
        if self.controller.screen_position.precise_distance_reference_index == 1 then
            -- absolute position
            fulcrum_position = self.controller.screen_position.position
        elseif self.controller.screen_position.precise_distance_reference_index == 2 then
            -- distance from left corner
            fulcrum_position = self.controller.screen_position.position + min_pos
        elseif self.controller.screen_position.precise_distance_reference_index == 3 then
            -- distance from right corner
            fulcrum_position = max_pos - self.controller.screen_position.position
        end

        self.controller.screen_position.discrete_screen_position_value =
            math.floor(((fulcrum_position - min_pos) / (max_pos - min_pos)) * 12 + 0.5) - 6
    end

    if self.view.screen_position.pivot_type_index_changed or self.view.relative_distance.relative_distance_changed then
        need_refresh = true
    end

    -- verify screen position is within bounds
    if
        self.view.screen_position.position_changed or self.view.relative_distance.relative_distance_changed or
            self.view.relative_distance.discrete_relative_distance_preset_index_changed or
            self.view.screen_position.pivot_type_index_changed or
            self.view.screen_position.discrete_screen_position_changed or
            self.view.screen_position.discrete_screen_position_value_changed or
            self.view.screen_position.precise_distance_reference_index_changed
     then
        -- check the calculated positions to see if they are within screen bounds
        local screen_min = module.data.PositionParametersData.default_screen_position.min
        local screen_max = module.data.PositionParametersData.default_screen_position.max

        local new_pos1
        local new_pos2

        -- first, calculate the position of the fulcrum based on the distance reference
        local fulcrum_position = 0.0
        if self.controller.screen_position.precise_distance_reference_index == 1 then
            -- absolute position
            fulcrum_position = self.controller.screen_position.position
        elseif self.controller.screen_position.precise_distance_reference_index == 2 then
            -- distance from left corner
            fulcrum_position =
                self.controller.screen_position.position +
                module.data.PositionParametersData.default_screen_position.min
        elseif self.controller.screen_position.precise_distance_reference_index == 3 then
            -- distance from right corner
            fulcrum_position =
                module.data.PositionParametersData.default_screen_position.max -
                self.controller.screen_position.position
        end

        -- calculate the new positions based on the pivot type
        if self.controller.screen_position.pivot_type_index == 1 then
            -- center pivot type
            new_pos1 = fulcrum_position - (self.controller.relative_distance.relative_distance / 2.0)
            new_pos2 = fulcrum_position + (self.controller.relative_distance.relative_distance / 2.0)
        elseif self.controller.screen_position.pivot_type_index == 2 then
            -- p1 player pivot type
            new_pos1 = fulcrum_position
            new_pos2 = fulcrum_position + self.controller.relative_distance.relative_distance
        elseif self.controller.screen_position.pivot_type_index == 3 then
            -- p2 player pivot type
            new_pos1 = fulcrum_position - self.controller.relative_distance.relative_distance
            new_pos2 = fulcrum_position
        end

        -- update the discrete slider value so the UI matches the computed fulcrum position
        local min_pos = module.data.PositionParametersData.default_screen_position.min
        local max_pos = module.data.PositionParametersData.default_screen_position.max
        local fulcrum_relative_position = fulcrum_position - min_pos
        local relative_range = max_pos - min_pos
        local discrete_value = math.floor((fulcrum_relative_position / relative_range) * 12 + 0.5) - 6
        self.controller.screen_position.discrete_screen_position_value = discrete_value

        -- check screen bounds
        if new_pos1 < screen_min or new_pos2 > screen_max then
            self.view.screen_position.show_position_warning = true
        else
            self.view.screen_position.show_position_warning = false
        end
    end

    -- randomizer synchronize bounds
    if self.view.randomizer.relative_discrete_bounds_changed == true then
        -- synchronize precise bounds to discrete bounds
        local lower_index = self.controller.randomizer.relative_lower_bound_discrete_index
        local upper_index = self.controller.randomizer.relative_upper_bound_discrete_index

        -- if either index is == max range, then we don't add by the minimum value, just just use the max

        local lower_value
        local upper_value

        if lower_index ~= #module.data.PositionParametersData.preset_relative_distance_offsets.values + 1 then
            lower_value = module.data.PositionParametersData.preset_relative_distance_offsets.values[lower_index]
        else
            lower_value = self.controller.relative_distance.max - self.controller.relative_distance.min
        end

        if upper_index ~= #module.data.PositionParametersData.preset_relative_distance_offsets.values + 1 then
            upper_value = module.data.PositionParametersData.preset_relative_distance_offsets.values[upper_index]
        else
            upper_value = self.controller.relative_distance.max - self.controller.relative_distance.min
        end

        self.controller.randomizer.relative_lower_bound = self.controller.relative_distance.min + lower_value
        self.controller.randomizer.relative_upper_bound = self.controller.relative_distance.min + upper_value
    end

    -- screen position bounds updating to match discrete/precise mode
    if self.view.randomizer.screen_discrete_bounds_changed == true then
        -- synchronize precise bounds to discrete bounds
        local lower_discrete = self.controller.randomizer.screen_lower_bound_discrete
        local upper_discrete = self.controller.randomizer.screen_upper_bound_discrete

        local min_pos = module.data.PositionParametersData.default_screen_position.min
        local max_pos = module.data.PositionParametersData.default_screen_position.max

        -- convert discrete to absolute positions
        local lower_value = min_pos + ((lower_discrete + 6) / 12) * (max_pos - min_pos)
        local upper_value = min_pos + ((upper_discrete + 6) / 12) * (max_pos - min_pos)

        -- based on the current reference type, convert to appropriate stored value
        if self.controller.screen_position.precise_distance_reference_index == 1 then
            -- absolute position
            self.controller.randomizer.screen_lower_bound = lower_value
            self.controller.randomizer.screen_upper_bound = upper_value
        elseif self.controller.screen_position.precise_distance_reference_index == 2 then
            -- distance from left corner
            self.controller.randomizer.screen_lower_bound =
                lower_value - module.data.PositionParametersData.default_screen_position.min
            self.controller.randomizer.screen_upper_bound =
                upper_value - module.data.PositionParametersData.default_screen_position.min
        elseif self.controller.screen_position.precise_distance_reference_index == 3 then
            -- distance from right corner (remember to invert)
            -- distance from right corner
            self.controller.randomizer.screen_upper_bound =
                module.data.PositionParametersData.default_screen_position.max - lower_value
            self.controller.randomizer.screen_lower_bound =
                module.data.PositionParametersData.default_screen_position.max - upper_value
        end
    end

    -- screen position precise bounds changed
    if self.view.randomizer.screen_continuous_bounds_changed == true then
        -- synchronize discrete bounds to precise bounds
        local min_pos = module.data.PositionParametersData.default_screen_position.min
        local max_pos = module.data.PositionParametersData.default_screen_position.max

        local lower_value
        local upper_value

        -- convert from stored value to absolute position
        if self.controller.screen_position.precise_distance_reference_index == 1 then
            -- absolute position
            lower_value = self.controller.randomizer.screen_lower_bound
            upper_value = self.controller.randomizer.screen_upper_bound
        elseif self.controller.screen_position.precise_distance_reference_index == 2 then
            -- distance from left corner
            lower_value =
                self.controller.randomizer.screen_lower_bound +
                module.data.PositionParametersData.default_screen_position.min
            upper_value =
                self.controller.randomizer.screen_upper_bound +
                module.data.PositionParametersData.default_screen_position.min
        elseif self.controller.screen_position.precise_distance_reference_index == 3 then
            -- distance from right corner
            upper_value =
                module.data.PositionParametersData.default_screen_position.max -
                self.controller.randomizer.screen_lower_bound
            lower_value =
                module.data.PositionParametersData.default_screen_position.max -
                self.controller.randomizer.screen_upper_bound
        end

        -- convert absolute positions to discrete values
        self.controller.randomizer.screen_lower_bound_discrete =
            math.floor(((lower_value - min_pos) / (max_pos - min_pos)) * 12 + 0.5) - 6
        self.controller.randomizer.screen_upper_bound_discrete =
            math.floor(((upper_value - min_pos) / (max_pos - min_pos)) * 12 + 0.5) - 6
    end

    return need_refresh
end

function PositionalParam:randomize()
    -- positional parameter randomization logic

    local need_refresh = false

    -- randomize relative distance
    if self.controller.randomizer.enabled_relative then
        need_refresh = true

        if self.controller.relative_distance.discrete_enabled then
            -- discrete preset randomization

            local lower_index = 1
            local upper_index = #module.data.PositionParametersData.preset_relative_distance_offsets.values + 1

            if self.controller.randomizer.relative_bounds_enabled then
                lower_index = self.controller.randomizer.relative_lower_bound_discrete_index
                upper_index = self.controller.randomizer.relative_upper_bound_discrete_index
            end

            local random_index = math.random(lower_index, upper_index)
            self.controller.relative_distance.discrete_relative_distance_preset_index = random_index

            -- set the actual relative distance value
            if random_index ~= #module.data.PositionParametersData.preset_relative_distance_offsets.values + 1 then
                self.controller.relative_distance.relative_distance =
                    self.controller.relative_distance.min +
                    module.data.PositionParametersData.preset_relative_distance_offsets.values[random_index]
            else
                self.controller.relative_distance.relative_distance = self.controller.relative_distance.max
            end
        else
            -- precise randomization
            local lower_bound = math.floor(self.controller.relative_distance.min)
            local upper_bound = math.floor(self.controller.relative_distance.max)

            if self.controller.randomizer.relative_bounds_enabled then
                lower_bound = math.floor(self.controller.randomizer.relative_lower_bound)
                upper_bound = math.floor(self.controller.randomizer.relative_upper_bound)
            end

            local random_value = math.random(lower_bound, upper_bound)
            self.controller.relative_distance.relative_distance = random_value
        end
    end

    if self.controller.randomizer.enabled_screen then
        need_refresh = true

        if self.controller.screen_position.discrete_screen_position then
            -- discrete preset randomization
            local lower_value = -6
            local upper_value = 6

            if self.controller.randomizer.screen_bounds_enabled then
                lower_value = self.controller.randomizer.screen_lower_bound_discrete
                upper_value = self.controller.randomizer.screen_upper_bound_discrete
            end

            local random_value = math.random(lower_value, upper_value)
            self.controller.screen_position.discrete_screen_position_value = random_value

            -- convert to absolute position
            local min_pos = module.data.PositionParametersData.default_screen_position.min
            local max_pos = module.data.PositionParametersData.default_screen_position.max
            local fulcrum_position = min_pos + ((random_value + 6) / 12) * (max_pos - min_pos)
            -- store controller.screen_position.position according to the currently selected reference type
            -- 1 = absolute position, 2 = distance from left corner, 3 = distance from right corner
            if self.controller.screen_position.precise_distance_reference_index == 1 then
                -- absolute
                self.controller.screen_position.position = fulcrum_position
            elseif self.controller.screen_position.precise_distance_reference_index == 2 then
                -- distance from left corner
                self.controller.screen_position.position = fulcrum_position - min_pos
            elseif self.controller.screen_position.precise_distance_reference_index == 3 then
                -- distance from right corner
                self.controller.screen_position.position = max_pos - fulcrum_position
            end
        else
            -- precise randomization
            local lower_bound = module.data.PositionParametersData.default_screen_position.min
            local upper_bound = module.data.PositionParametersData.default_screen_position.max

            if self.controller.randomizer.screen_bounds_enabled then
                lower_bound = self.controller.randomizer.screen_lower_bound
                upper_bound = self.controller.randomizer.screen_upper_bound
            end

            local random_value = math.random(math.floor(lower_bound), math.floor(upper_bound))
            self.controller.screen_position.position = random_value

            -- update the discrete slider value so the UI matches the computed fulcrum position
            local fulcrum_position = 0.0
            if self.controller.screen_position.precise_distance_reference_index == 1 then
                -- absolute position
                fulcrum_position = self.controller.screen_position.position
            elseif self.controller.screen_position.precise_distance_reference_index == 2 then
                -- distance from left corner
                fulcrum_position =
                    self.controller.screen_position.position +
                    module.data.PositionParametersData.default_screen_position.min
            elseif self.controller.screen_position.precise_distance_reference_index == 3 then
                -- distance from right corner
                fulcrum_position =
                    module.data.PositionParametersData.default_screen_position.max -
                    self.controller.screen_position.position
            end

            self.controller.screen_position.discrete_screen_position_value =
                math.floor(
                ((fulcrum_position - module.data.PositionParametersData.default_screen_position.min) /
                    (module.data.PositionParametersData.default_screen_position.max -
                        module.data.PositionParametersData.default_screen_position.min)) *
                    12 +
                    0.5
            ) - 6
        end
    end

    -- if the randomizer is enabled, then randomly choose a side
    if self.controller.randomizer.sideswitch_enabled then
        self.model.Is_Side_Flip = (math.random(0, 1) == 1)
    end

    return need_refresh
end

function PositionalParam:draw_ui()
    -- positional parameter UI logic

    _, self.controller.randomizer.sideswitch_enabled =
        imgui.checkbox("Enable Random Side Switching", self.controller.randomizer.sideswitch_enabled)

    self.view.relative_distance.relative_distance_enabled_changed, self.controller.relative_distance.enabled =
        imgui.checkbox("Enable Start Position Adjustment", self.controller.relative_distance.enabled)

    imgui.separator()

    if self.controller.relative_distance.enabled then
        self.controller.relative_distance.discrete_relative_distance_enabled_changed,
            self.controller.relative_distance.discrete_enabled =
            imgui.checkbox("Use Preset Relative Distances", self.controller.relative_distance.discrete_enabled)

        -- randomizer
        if self.controller.randomizer.enabled_relative then
            imgui.begin_disabled()
        end

        if self.controller.relative_distance.discrete_enabled then
            -- combo picker for preset relative distances
            self.view.relative_distance.discrete_relative_distance_preset_index_changed,
                self.controller.relative_distance.discrete_relative_distance_preset_index =
                imgui.combo(
                "Relative Distance Presets",
                self.controller.relative_distance.discrete_relative_distance_preset_index,
                module.data.PositionParametersData.preset_relative_distance_offsets.names
            )
            imgui.text(
                "Current Relative Distance: " ..
                    string.format("%.2f", self.controller.relative_distance.relative_distance)
            )
        else
            self.view.relative_distance.relative_distance_changed, self.controller.relative_distance.relative_distance =
                imgui.drag_float(
                "Relative Distance",
                self.controller.relative_distance.relative_distance,
                1.0,
                self.controller.relative_distance.min,
                self.controller.relative_distance.max
            )
            if imgui.is_item_active() then
                module.ui_active = true
            end
        end

        if self.controller.randomizer.enabled_relative then
            imgui.end_disabled()
        end

        -- randomizer checkbox
        _, self.controller.randomizer.enabled_relative =
            imgui.checkbox("Toggle Relative Distance Randomization", self.controller.randomizer.enabled_relative)

        if self.controller.randomizer.enabled_relative then
            _, self.controller.randomizer.relative_bounds_enabled =
                imgui.checkbox(
                "Enable Bounds for Relative Distance Randomization",
                self.controller.randomizer.relative_bounds_enabled
            )
            if self.controller.randomizer.relative_bounds_enabled then
                -- depending on the discrete/precise mode, show the appropriate bound controls
                if self.controller.relative_distance.discrete_enabled then
                    -- discrete preset bounds
                    self.view.randomizer.relative_discrete_bounds_changed = false

                    -- for the discrete case, we only show a subset of the presets that fit within the current indices
                    local lower_bound_options = {}
                    local upper_bound_options = {}
                    -- maps from local combo index -> global preset index
                    local lower_map = {}
                    local upper_map = {}

                    local full_names = module.data.PositionParametersData.preset_relative_distance_offsets.names

                    -- build option lists and mapping tables. We need to translate the controller's
                    -- stored global indices into local indices that match the reduced option lists
                    local local_lower_selected = 1
                    local local_upper_selected =
                        #module.data.PositionParametersData.preset_relative_distance_offsets.names
                    for i, name in ipairs(full_names) do
                        if i <= self.controller.randomizer.relative_upper_bound_discrete_index then
                            table.insert(lower_bound_options, name)
                            table.insert(lower_map, i)
                            if i == self.controller.randomizer.relative_lower_bound_discrete_index then
                                local_lower_selected = #lower_bound_options
                            end
                        end
                        if i >= self.controller.randomizer.relative_lower_bound_discrete_index then
                            table.insert(upper_bound_options, name)
                            table.insert(upper_map, i)
                            if i == self.controller.randomizer.relative_upper_bound_discrete_index then
                                local_upper_selected = #upper_bound_options
                            end
                        end
                    end

                    -- call imgui with local indices
                    local changed_lower, new_local_lower =
                        imgui.combo(
                        "Relative Distance Randomization Lower Bound Preset",
                        local_lower_selected,
                        lower_bound_options
                    )
                    if changed_lower then
                        -- map back to the global preset index
                        self.controller.randomizer.relative_lower_bound_discrete_index = lower_map[new_local_lower]
                        self.view.randomizer.relative_discrete_bounds_changed = true
                    end

                    local changed_upper, new_local_upper =
                        imgui.combo(
                        "Relative Distance Randomization Upper Bound Preset",
                        local_upper_selected,
                        upper_bound_options
                    )
                    if changed_upper then
                        self.controller.randomizer.relative_upper_bound_discrete_index = upper_map[new_local_upper]
                        self.view.randomizer.relative_discrete_bounds_changed = true
                    end
                else
                    -- precise bounds
                    _, self.controller.randomizer.relative_lower_bound =
                        imgui.drag_float(
                        "Relative Distance Randomization Lower Bound",
                        self.controller.randomizer.relative_lower_bound,
                        1,
                        self.controller.relative_distance.min,
                        self.controller.randomizer.relative_upper_bound
                    )
                    _, self.controller.randomizer.relative_upper_bound =
                        imgui.drag_float(
                        "Relative Distance Randomization Upper Bound",
                        self.controller.randomizer.relative_upper_bound,
                        1,
                        self.controller.randomizer.relative_lower_bound,
                        self.controller.relative_distance.max
                    )
                end
            end
        end

        imgui.separator()

        -- checkbox for enabling starting position adjustment
        self.view.screen_position.enabled_changed, self.controller.screen_position.enabled =
            imgui.checkbox("Enable Starting Position Adjustment", self.controller.screen_position.enabled)
        if self.controller.screen_position.enabled then
            -- combo picker for type of starting position
            self.view.screen_position.pivot_type_index_changed, self.controller.screen_position.pivot_type_index =
                imgui.combo(
                "Starting Position Pivot Type",
                self.controller.screen_position.pivot_type_index,
                {
                    "Center Pivot Point",
                    "Left Player Position Pivot (P1 Side)",
                    "Right Player Position Pivot (P2 Side)"
                }
            )

            imgui.spacing()

            -- checkbox for discrete reference point adjustment toggle
            self.view.screen_position.discrete_screen_position_changed,
                self.controller.screen_position.discrete_screen_position =
                imgui.checkbox(
                "Use Discrete Starting Position Reference",
                self.controller.screen_position.discrete_screen_position
            )

            if self.controller.screen_position.discrete_screen_position then
                -- randomizer disabling
                if self.controller.randomizer.enabled_screen then
                    imgui.begin_disabled()
                end

                -- slider for discrete reference point
                self.view.screen_position.discrete_screen_position_value_changed,
                    self.controller.screen_position.discrete_screen_position_value =
                    imgui.slider_int(
                    "Starting Position Reference",
                    self.controller.screen_position.discrete_screen_position_value,
                    -6,
                    6
                )
                if imgui.is_item_active() then
                    module.ui_active = true
                end

                if self.controller.randomizer.enabled_screen then
                    imgui.end_disabled()
                end
            else
                -- combo picker for distance reference
                self.view.screen_position.precise_distance_reference_index_changed,
                    self.controller.screen_position.precise_distance_reference_index =
                    imgui.combo(
                    "Starting Position Distance Reference",
                    self.controller.screen_position.precise_distance_reference_index,
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
                if self.controller.screen_position.precise_distance_reference_index == 1 then
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

                -- randomizer disabling
                if self.controller.randomizer.enabled_screen then
                    imgui.begin_disabled()
                end

                self.view.screen_position.position_changed, self.controller.screen_position.position =
                    imgui.drag_float(
                    "Starting Position X",
                    self.controller.screen_position.position,
                    1.0,
                    starting_position_min,
                    starting_position_max
                )
                if imgui.is_item_active() then
                    module.ui_active = true
                end

                if self.controller.randomizer.enabled_screen then
                    imgui.end_disabled()
                end
            end

            -- randomizer checkbox
            _, self.controller.randomizer.enabled_screen =
                imgui.checkbox("Toggle Starting Position Randomization", self.controller.randomizer.enabled_screen)
            if self.controller.randomizer.enabled_screen then
                _, self.controller.randomizer.screen_bounds_enabled =
                    imgui.checkbox(
                    "Enable Bounds for Starting Position Randomization",
                    self.controller.randomizer.screen_bounds_enabled
                )

                if self.controller.randomizer.screen_bounds_enabled then
                    -- depending on the discrete/precise mode, show the appropriate bound controls
                    if self.controller.screen_position.discrete_screen_position then
                        -- discrete values from -6 to 6

                        self.view.randomizer.screen_discrete_bounds_changed = false

                        local changed = false

                        changed, self.controller.randomizer.screen_lower_bound_discrete =
                            imgui.drag_int(
                            "Starting Position Randomization Lower Bound Discrete",
                            self.controller.randomizer.screen_lower_bound_discrete,
                            0.3,
                            -6,
                            self.controller.randomizer.screen_upper_bound_discrete
                        )
                        if changed then
                            self.view.randomizer.screen_discrete_bounds_changed = true
                        end

                        changed, self.controller.randomizer.screen_upper_bound_discrete =
                            imgui.drag_int(
                            "Starting Position Randomization Upper Bound Discrete",
                            self.controller.randomizer.screen_upper_bound_discrete,
                            0.3,
                            self.controller.randomizer.screen_lower_bound_discrete,
                            6
                        )
                        if changed then
                            self.view.randomizer.screen_discrete_bounds_changed = true
                        end
                    else
                        -- precise bounds
                        -- starting position bounds
                        local starting_position_min = 0.0
                        local starting_position_max = 0.0
                        if self.controller.screen_position.precise_distance_reference_index == 1 then
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

                        self.view.randomizer.screen_continuous_bounds_changed = false

                        local changed = false

                        changed, self.controller.randomizer.screen_lower_bound =
                            imgui.drag_float(
                            "Starting Position Randomization Lower Bound",
                            self.controller.randomizer.screen_lower_bound,
                            1,
                            starting_position_min,
                            self.controller.randomizer.screen_upper_bound
                        )
                        if changed then
                            self.view.randomizer.screen_continuous_bounds_changed = true
                        end

                        changed, self.controller.randomizer.screen_upper_bound =
                            imgui.drag_float(
                            "Starting Position Randomization Upper Bound",
                            self.controller.randomizer.screen_upper_bound,
                            1,
                            self.controller.randomizer.screen_lower_bound,
                            starting_position_max
                        )
                        if changed then
                            self.view.randomizer.screen_continuous_bounds_changed = true
                        end
                    end
                end
            end

            if self.view.screen_position.show_position_warning then
                imgui.text_colored(
                    "Warning: The current combination of relative distance and starting position would cause the character to go offscreen!\nThe position will be adjust to respect the relative position and screen bounds.",
                    0xFF00A9F9
                )
            end
        end
    end
end

--[[
    Module level logic
]]
function module.init()
    -- load the game parameters
    module.data.TrainingManager = sdk.get_managed_singleton("app.training.TrainingManager")
    module.data.TrainingData = module.data.TrainingManager:get_field("_tData")
    module.data.ParameterSetting = module.data.TrainingData:get_field("ParameterSetting")
    module.data.SelectMenu = module.data.TrainingData:get_field("SelectMenu")
    module.data.tf_PS = module.data.TrainingManager._tfFuncs._entries[6]:get_field("value")
    -- module.data.refresh_object =
    --     module.data.TrainingManager._tfFuncs._entries[0]:get_field("value"):get_field("FuncList")
    local gBattle = sdk.find_type_definition("gBattle")
    local sPlayer = gBattle:get_field("Player"):get_data(nil)
    local cPlayer = sPlayer.mcPlayer
    -- use sGame.stage_timer == 1 to check for the refresh (you can apply all the settings you want here and they won't get overwritten by the game at this point)
    module.data.sGame = gBattle:get_field("Game"):get_data(nil)
    module.data.live_P1 = cPlayer[0]
    module.data.live_P2 = cPlayer[1]

    -- initialize player parameters
    PlayerParam:init(module.data.ParameterSetting)
    UniqueGaugeParam:init(module.data.ParameterSetting.UniqueData)
    PositionalParam:init(module.data.SelectMenu)

    -- load config file
    local config_file = json.load_file("TrainingModePlus/TrainingSettingsAndRandomizer_Config.json")
    if config_file ~= nil then
        PlayerParam.controller = config_file.PlayerParam or PlayerParam.controller
        PositionalParam.controller = config_file.PositionalParam or PositionalParam.controller
        module.hotkeys = config_file.Hotkeys or module.hotkeys

        -- Safely merge UniqueGaugeParam data instead of overwriting it (useful for updating mod versions where new characters may be added)
        if config_file.UniqueGaugeParam then
            for char_name, char_data in pairs(config_file.UniqueGaugeParam) do
                -- Only copy if the character exists in the current initialized data
                if UniqueGaugeParam.controller[char_name] then
                    for gauge_id, gauge_data in pairs(char_data) do
                        if UniqueGaugeParam.controller[char_name][gauge_id] then
                            for key, val in pairs(gauge_data) do
                                UniqueGaugeParam.controller[char_name][gauge_id][key] = val
                            end
                        end
                    end
                end
            end
        end
    end

    -- initialize refresh request flag
    module.request_refresh = false
    module.ui_active = false

    Hotkey.setup_hotkeys(module.hotkeys, module.default_hotkeys)
end

function module.on_frame()
    -- module logic goes here
    local need_apply = false
    local need_refresh = false
    local request_randomizer = false

    -- randomization logic
    -- for now just use this, later set this to a bind or something
    module.request_randomizer = module.request_randomizer or Hotkey.check_hotkey("request_randomizer", nil, true)

    if module.request_randomizer then
        -- randomize parameters
        PlayerParam:randomize()
        UniqueGaugeParam:randomize()
        need_refresh = PositionalParam:randomize() or need_refresh

        need_refresh = true
        module.request_randomizer = false
    end

    need_apply = PlayerParam:update() or need_apply
    need_refresh = UniqueGaugeParam:update() or need_refresh
    need_refresh = PositionalParam:update() or need_refresh

    -- apply the settings if needed
    if need_apply then
        sdk.call_object_func(module.data.tf_PS, "bApply")
    end

    if need_refresh then
        module.request_refresh = true
    end

    if module.request_refresh == true and (module.ui_active == false) then
        module.data.TrainingManager._IsReqRefresh = true
        PositionalParam:update_positioning_func()
        module.request_refresh = false
    end
    module.ui_active = false
end

function module.draw_ui()
    -- module level UI
    if imgui.collapsing_header("Training Settings + Randomizer") then
        module.request_randomizer = imgui.button("Refresh and Randomize")

        imgui.same_line()
        local hotkeyChanged = Hotkey.hotkey_setter("request_randomizer", nil, "Hotkey")

        if hotkeyChanged then
            Hotkey.update_hotkey_table(module.hotkeys)
        end

        -- player specific UI
        PlayerParam:draw_ui()
        if imgui.tree_node("Unique Character Gauges") then
            UniqueGaugeParam:draw_ui()
            imgui.tree_pop()
        end
        if imgui.tree_node("Positional Parameters") then
            PositionalParam:draw_ui()
            imgui.tree_pop()
        end
    end
end

function module.on_exit_training_mode()
    -- save the config
    config_file = {
        PlayerParam = PlayerParam.controller,
        UniqueGaugeParam = UniqueGaugeParam.controller,
        PositionalParam = PositionalParam.controller,
        Hotkeys = module.hotkeys
    }

    json.dump_file("TrainingModePlus/TrainingSettingsAndRandomizer_Config.json", config_file)
end

return module

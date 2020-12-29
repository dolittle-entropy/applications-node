import 'reflect-metadata';

import React from 'react';
import ReactDOM from 'react-dom';

import { Bootstrapper } from '@dolittle/vanir-react';
import { VersionInfo } from '@dolittle/vanir-web';

const version = require('../microservice.json') as VersionInfo;

import '@shared/styles/theme';
import './index.scss';

export default function App() {
    return (
        <>
        </>
    );
}

ReactDOM.render(
    <Bootstrapper name="{{pascalCase name}}" prefix="{{lowerCase uiPrefix}}" version={version}>
        <App />
    </Bootstrapper>,
    document.getElementById('root')
);
